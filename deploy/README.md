# WSL + DuckDNS 운영 배포 가이드

## 구성

운영 환경은 별도 VPS 없이 Windows PC의 WSL 2 Ubuntu x86_64에서 실행합니다. ASP.NET Core, PostgreSQL, Caddy는 Docker 컨테이너로 실행합니다.

- GitHub Actions → Tailscale SSH → WSL의 `deploy` 사용자
- 인터넷 → `wildlife-sports.duckdns.org` → 공유기 포트포워딩 → Caddy
- Caddy → Docker 내부 ASP.NET 앱
- ASP.NET 앱과 migrator → Docker 내부 PostgreSQL `db:5432`

외부에는 Caddy의 TCP 80/443만 공개합니다. PostgreSQL 5432, 앱 8080, 로컬 검증 포트 18080, SSH 22는 공유기에서 포워딩하지 않습니다.

| 브랜치 | 동작 | WSL 변경 |
| --- | --- | --- |
| `develop` | CI 후 app/migrator 개발 이미지를 GHCR에 게시 | 없음 |
| `release/**` | Actions에서 임시 Compose 배포와 migration·health 검증 | 없음 |
| `main` | 운영 이미지를 GHCR에 게시하고 Tailscale SSH로 WSL에 배포 | 있음 |

`v*` 태그와 GitHub Release는 자동 배포 트리거가 아닙니다.

## 반드시 알아야 할 제약

- Windows PC가 종료·재부팅·절전되면 게임 서버도 중단됩니다.
- Docker Desktop과 WSL이 실행 중일 때만 GitHub Actions가 배포할 수 있습니다.
- DuckDNS는 DNS만 제공하므로 서버 트래픽을 대신 중계하지 않습니다.
- 인터넷 회선에 외부 접속 가능한 공인 IPv4가 필요합니다. CGNAT 회선에서는 공유기 포트포워딩만으로 공개할 수 없습니다.
- 공인 IP가 바뀌면 DuckDNS 레코드도 갱신해야 합니다.
- 이 구성은 무료 소규모 운영용이며 고가용성 환경이 아닙니다.

## 1. Windows와 WSL 확인

Windows PowerShell에서 WSL 버전과 배포판 이름을 확인합니다.

```powershell
wsl --version
wsl --list --verbose
```

WSL 2가 아니면 먼저 갱신합니다.

```powershell
wsl --update
```

Docker Desktop을 설치하고 `Settings > Resources > WSL Integration`에서 현재 Ubuntu 배포판을 활성화합니다. Docker Desktop의 로그인 시 자동 시작 옵션도 켭니다.

WSL에서 systemd를 확인합니다.

```bash
systemctl status
```

systemd가 아니라면 `/etc/wsl.conf`에 다음 값을 설정한 뒤 Windows PowerShell에서 `wsl --shutdown`을 실행하고 WSL을 다시 엽니다.

```ini
[boot]
systemd=true
```

## 2. WSL 필수 도구와 Tailscale 설치

다음 명령은 Windows PowerShell이 아닌 WSL Ubuntu 터미널에서 실행합니다.

```bash
sudo apt-get update
sudo apt-get install --yes curl sudo util-linux
curl -fsSL https://tailscale.com/install.sh | sh
sudo tailscale up
```

출력된 URL에서 Tailscale 로그인을 완료한 후 Tailscale 관리 콘솔의 Machines에서 이 WSL 노드에 `tag:wildlife-prod`를 지정합니다. Windows에 설치된 Tailscale 노드가 아니라 WSL 내부 노드를 선택해야 합니다.

Tailscale SSH를 활성화합니다.

```bash
sudo tailscale set --ssh
```

Tailscale은 공개 API가 아니라 GitHub Actions의 비공개 SSH 배포 통로로만 사용합니다.

## 3. 배포 파일 설치

저장소 루트의 WSL 터미널에서 bootstrap을 실행합니다.

```bash
sudo bash deploy/server/bootstrap.sh
```

bootstrap은 다음 항목을 설치합니다.

- `/opt/wildlife/infra`: Compose와 Caddy 설정
- `/usr/local/sbin/wildlife-deploy`: 검증·백업·배포·롤백 명령
- `/etc/sudoers.d/wildlife-deploy`: `apply`, `rollback`만 허용하는 sudo 규칙
- `/etc/wildlife`: 권한 600의 운영 환경 파일
- `/var/backups/wildlife`: 저장소 밖 DB dump 디렉터리

이미 존재하는 `/etc/wildlife/db.env`, `app.env`, `caddy.env`는 덮어쓰지 않습니다.

## 4. 운영 환경변수 설정

실제 값은 저장소나 셸 명령 인자에 기록하지 않고 `sudoedit`로 입력합니다.

`/etc/wildlife/db.env`:

```env
POSTGRES_DB=
POSTGRES_USER=
POSTGRES_PASSWORD=
```

`/etc/wildlife/app.env`:

```env
ConnectionStrings__DefaultConnection=
Gmail__Address=
Gmail__AppPassword=
```

연결 문자열은 호스트 `db`, 포트 `5432`, 그리고 `db.env`와 동일한 DB 이름·사용자·비밀번호로 구성합니다. 공개 URL이나 `localhost`를 DB 호스트로 사용하지 않습니다.

`/etc/wildlife/caddy.env`:

```env
APP_DOMAIN=wildlife-sports.duckdns.org
```

권한을 확인합니다.

```bash
sudo stat -c '%U:%G %a %n' /etc/wildlife/*.env
```

세 파일은 모두 `root:root 600`이어야 합니다.

## 5. 비공개 GHCR 로그인

`read:packages`만 가진 GitHub PAT classic을 사용합니다. PAT를 만든 개인 GitHub 사용자명을 사용하며 토큰은 표준 입력으로만 전달합니다.

```bash
read -r -s GHCR_READ_TOKEN
printf '%s' "${GHCR_READ_TOKEN}" | sudo docker login ghcr.io --username '<github-user>' --password-stdin
unset GHCR_READ_TOKEN
```

`Login Succeeded`가 나오면 완료입니다. root의 Docker 인증 파일에 저장되는 이유는 root 소유 배포 명령이 GHCR 이미지를 pull하기 때문입니다.

## 6. DuckDNS와 공유기 설정

DuckDNS의 `wildlife-sports.duckdns.org` 레코드를 현재 인터넷 회선의 공인 IPv4로 갱신합니다. 공인 IP가 동적으로 바뀌는 회선이라면 다음 중 하나로 자동 갱신합니다.

- 공유기가 DuckDNS 또는 사용자 정의 DDNS 갱신을 지원하면 공유기에 설정
- 지원하지 않으면 DuckDNS가 제공하는 Linux 갱신 스크립트나 별도 DDNS 클라이언트 사용

DuckDNS 토큰은 저장소, GitHub Actions 로그, Caddyfile에 기록하지 않습니다.

공유기에서 Windows PC의 고정 LAN IPv4 또는 DHCP 예약 주소로 다음 포트를 전달합니다.

```text
WAN TCP 80  → Windows PC TCP 80
WAN TCP 443 → Windows PC TCP 443
WAN UDP 443 → Windows PC UDP 443  (HTTP/3, 선택)
```

Windows 방화벽에서도 인바운드 TCP 80/443과 선택적으로 UDP 443을 허용합니다. TCP 22, 5432, 8080, 18080은 열지 않습니다.

포트포워딩을 마친 뒤 휴대전화 Wi-Fi를 끄고 모바일 데이터에서 다음 주소가 열리는지 확인합니다. 같은 LAN에서는 공유기의 NAT loopback 지원 여부 때문에 잘못 실패할 수 있습니다.

```text
https://wildlife-sports.duckdns.org/api/health
```

Caddy는 최초 기동 시 이 도메인의 공개 TLS 인증서를 자동 발급하고 `/data` 볼륨에 보존합니다. 인증서 발급을 위해 DuckDNS가 올바른 공인 IP를 가리키고 외부 TCP 80 또는 443이 Caddy까지 도달해야 합니다.

## 7. Tailscale 정책

[tailscale-policy.hujson](examples/tailscale-policy.hujson)의 항목을 기존 tailnet 정책에 병합합니다. 기존 정책 전체를 예제 파일로 덮어쓰지 않습니다.

- `tag:wildlife-ci`에서 `tag:wildlife-prod` TCP 22 접근
- CI 노드가 WSL의 `deploy` 사용자로만 SSH

Funnel은 사용하지 않으므로 `tag:wildlife-prod`에 추가했던 `funnel` node attribute는 정책에서 제거해도 됩니다. Tailscale의 공인 22번 포트는 열지 않습니다.

## 8. GitHub production Environment

GitHub 저장소에 `production` Environment를 만들고 배포 브랜치를 `main`으로 제한합니다.

Environment secrets:

```text
TS_OAUTH_CLIENT_ID
TS_AUDIENCE
```

Environment variables:

```text
TS_HOST=<WSL 노드의 Tailscale MagicDNS 이름 또는 Tailscale IP>
TS_SSH_USER=deploy
```

Tailscale Workload Identity Federation은 현재 GitHub 저장소와 `production` Environment의 OIDC claim만 허용하고 생성 노드에 `tag:wildlife-ci`를 부여합니다. 장기 Tailscale auth key나 SSH 개인키는 GitHub에 저장하지 않습니다.

## 9. WSL 실행 상태 유지

배포하거나 게임 서버를 공개하는 동안 Windows 절전을 끄고 Docker Desktop을 실행합니다. WSL이 종료되지 않도록 Windows PowerShell에서 다음 명령을 실행해 둘 수 있습니다.

```powershell
wsl --distribution Ubuntu --exec /bin/sleep infinity
```

이 PowerShell 창을 닫거나 Windows가 절전·종료되면 자동 배포와 공개 API가 중단될 수 있습니다. 재부팅 후에는 Docker Desktop, WSL, `tailscaled`, Caddy 상태를 확인합니다.

```bash
systemctl status tailscaled
sudo docker compose --env-file /opt/wildlife/state/current.env -f /opt/wildlife/infra/compose.production.yml ps
curl --fail --silent https://wildlife-sports.duckdns.org/api/health
```

## 배포 동작

`main` workflow는 app/migrator 이미지를 비공개 GHCR에 게시하고 이미지 digest가 포함된 요청 파일을 WSL에 전달합니다. 요청 파일에는 시크릿이 없으며 root 소유 명령이 저장소명과 SHA-256 형식을 검증합니다.

1. 배포 lock 획득과 이미지 pull
2. PostgreSQL 기동 및 health 확인
3. custom-format dump 생성과 `pg_restore --list` 검증
4. self-contained EF migration bundle 실행
5. 새 app/Caddy 기동
6. loopback 및 DuckDNS 공개 HTTPS `/api/health` 확인
7. 실패 시 이전 app digest 자동 복구

최초 배포에서는 `POSTGRES_DB` 값으로 빈 데이터베이스를 만든 뒤 커밋된 EF Core migration을 적용해 스키마를 생성합니다.

자동 롤백은 DB migration을 내리지 않습니다. 향후 migration은 이전 앱이 새 스키마에서도 기동할 수 있는 expand/contract 방식으로 작성해야 합니다.

## 수동 롤백과 DB 복구

애플리케이션만 이전 digest로 되돌릴 때는 GitHub Actions의 `Roll Back Production` workflow를 `main` 기준으로 수동 실행합니다.

DB 복원은 자동화하지 않습니다. 복원이 필요하면 앱 요청을 중단하고 `/var/backups/wildlife`의 대상 dump, 생성 시각, 현재 migration 호환성을 검토한 뒤 명시 승인된 복원 명령만 실행합니다. `pg_restore --clean`은 현재 데이터를 삭제할 수 있으므로 자동 workflow에서 실행하지 않습니다.

WSL 배포판 자체가 손상되면 내부 백업도 함께 유실될 수 있습니다. 실제 데이터가 쌓이기 시작하면 `/var/backups/wildlife`의 dump를 저장소 밖 Windows 디스크나 별도 저장소에 주기적으로 복사해야 합니다. DB dump와 환경 파일은 Git에 추가하지 않습니다.

## 인프라 설정 변경

`deploy` 사용자는 Docker 그룹에 속하지 않으며 root 소유 Compose/Caddy 파일을 수정할 수 없습니다. Compose, Caddy 또는 배포 스크립트가 바뀌면 최신 저장소에서 bootstrap을 다시 실행해 설치 파일을 갱신합니다.
