# WSL + GSMVS 운영 배포 가이드

## 구성

운영 환경은 별도 VPS 없이 Windows PC의 WSL 2 Ubuntu x86_64에서 실행합니다. ASP.NET Core, PostgreSQL, Caddy는 Docker 컨테이너로 실행합니다.

- GitHub Actions → Tailscale SSH → WSL의 `deploy` 사용자
- 인터넷 HTTPS → GSMVS → 서버 TCP 18080 → Caddy
- Caddy → Docker 내부 ASP.NET 앱
- ASP.NET 앱과 migrator → Docker 내부 PostgreSQL `db:5432`

GSMVS가 와일드카드 인증서로 공개 HTTPS를 종료하고 서버의 TCP 18080에 HTTP로 전달합니다. PostgreSQL 5432, 앱 8080, SSH 22와 Caddy의 80/443은 외부에 공개하지 않습니다.

| 브랜치 | 동작 | WSL 변경 |
| --- | --- | --- |
| `develop` | CI 후 app/migrator 개발 이미지를 GHCR에 게시 | 없음 |
| `release/**` | Actions에서 임시 Compose 배포와 migration·health 검증 | 없음 |
| `main` | 운영 이미지를 GHCR에 게시하고 Tailscale SSH로 WSL에 배포 | 있음 |

`v*` 태그와 GitHub Release는 자동 배포 트리거가 아닙니다.

## 반드시 알아야 할 제약

- Windows PC가 종료·재부팅·절전되면 게임 서버도 중단됩니다.
- Docker Desktop과 WSL이 실행 중일 때만 GitHub Actions가 배포할 수 있습니다.
- GSMVS 서브도메인과 내부 포트 연결이 활성화되어 있어야 공개 요청이 서버에 도달합니다.
- Caddy는 내부 HTTP 리버스 프록시로만 사용하며 공개 인증서를 발급하지 않습니다.
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

이미 존재하는 `/etc/wildlife/db.env`, `app.env`, `deploy.env`는 덮어쓰지 않습니다.

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

`/etc/wildlife/deploy.env`:

```env
PUBLIC_HOST=wildlife-sports-day.https.gsmsv.site
```

권한을 확인합니다.

```bash
sudo stat -c '%U:%G %a %n' /etc/wildlife/*.env
```

세 파일은 모두 `root:root 600`이어야 합니다. `deploy.env`의 공개 호스트명은 시크릿이 아니지만 배포 설정의 무단 변경을 막기 위해 동일한 권한을 사용합니다.

## 5. 비공개 GHCR 로그인

`read:packages`만 가진 GitHub PAT classic을 사용합니다. PAT를 만든 개인 GitHub 사용자명을 사용하며 토큰은 표준 입력으로만 전달합니다.

```bash
read -r -s GHCR_READ_TOKEN
printf '%s' "${GHCR_READ_TOKEN}" | sudo docker login ghcr.io --username '<github-user>' --password-stdin
unset GHCR_READ_TOKEN
```

`Login Succeeded`가 나오면 완료입니다. root의 Docker 인증 파일에 저장되는 이유는 root 소유 배포 명령이 GHCR 이미지를 pull하기 때문입니다.

## 6. GSMVS 도메인과 포트 설정

GSMVS에서 HTTPS 서브도메인을 추가하고 다음 값으로 서버에 연결합니다.

```text
공개 주소: wildlife-sports-day.https.gsmsv.site
출발 IP: 0.0.0.0/0
내부 IP: GSMVS 관리 화면에 표시되는 서버 내부 IP
내부 포트: 18080
내부 프로토콜: HTTP
```

Compose는 서버의 모든 인터페이스에서 TCP 18080을 받고 Caddy 컨테이너의 8080으로 전달합니다. GSMVS가 공개 TLS를 처리하므로 Caddy의 호스트 TCP 80/443과 UDP 443은 열지 않습니다.

Windows 방화벽에서 GSMVS 연결에 필요한 인바운드 TCP 18080을 허용합니다. 가능하면 GSMVS가 제공하는 프록시 원본 대역으로 제한하고, 공유기나 다른 장비에서 5432, 8080, 22를 공개하지 않습니다.

먼저 WSL 내부 상태를 확인한 뒤 공개 주소를 확인합니다.

```bash
curl --fail --silent http://127.0.0.1:18080/api/health
curl --fail --silent https://wildlife-sports-day.https.gsmsv.site/api/health
```

로컬 요청은 성공하지만 공개 요청이 502이면 GSMVS의 내부 포트가 18080인지, 운영 Compose가 `0.0.0.0:18080:8080`으로 설치되었는지, Windows 방화벽이 TCP 18080을 허용하는지 확인합니다.

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
curl --fail --silent https://wildlife-sports-day.https.gsmsv.site/api/health
```

## 배포 동작

`main` workflow는 app/migrator 이미지를 비공개 GHCR에 게시하고 이미지 digest가 포함된 요청 파일을 WSL에 전달합니다. 요청 파일에는 시크릿이 없으며 root 소유 명령이 저장소명과 SHA-256 형식을 검증합니다.

1. 배포 lock 획득과 이미지 pull
2. PostgreSQL 기동 및 health 확인
3. custom-format dump 생성과 `pg_restore --list` 검증
4. self-contained EF migration bundle 실행
5. 새 app/Caddy 기동
6. loopback 및 GSMVS 공개 HTTPS `/api/health` 확인
7. 실패 시 이전 app digest 자동 복구

최초 배포에서는 `POSTGRES_DB` 값으로 빈 데이터베이스를 만든 뒤 커밋된 EF Core migration을 적용해 스키마를 생성합니다.

자동 롤백은 DB migration을 내리지 않습니다. 향후 migration은 이전 앱이 새 스키마에서도 기동할 수 있는 expand/contract 방식으로 작성해야 합니다.

## 수동 롤백과 DB 복구

애플리케이션만 이전 digest로 되돌릴 때는 GitHub Actions의 `Roll Back Production` workflow를 `main` 기준으로 수동 실행합니다.

DB 복원은 자동화하지 않습니다. 복원이 필요하면 앱 요청을 중단하고 `/var/backups/wildlife`의 대상 dump, 생성 시각, 현재 migration 호환성을 검토한 뒤 명시 승인된 복원 명령만 실행합니다. `pg_restore --clean`은 현재 데이터를 삭제할 수 있으므로 자동 workflow에서 실행하지 않습니다.

WSL 배포판 자체가 손상되면 내부 백업도 함께 유실될 수 있습니다. 실제 데이터가 쌓이기 시작하면 `/var/backups/wildlife`의 dump를 저장소 밖 Windows 디스크나 별도 저장소에 주기적으로 복사해야 합니다. DB dump와 환경 파일은 Git에 추가하지 않습니다.

## 인프라 설정 변경

`deploy` 사용자는 Docker 그룹에 속하지 않으며 root 소유 Compose/Caddy 파일을 수정할 수 없습니다. Compose, Caddy 또는 배포 스크립트가 바뀌면 최신 저장소에서 bootstrap을 다시 실행해 설치 파일을 갱신합니다.
