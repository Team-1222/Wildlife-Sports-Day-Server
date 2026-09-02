# Wildlife-Sports-Day-Server
제한 시간 이내로 많은 미션을 해결하고 많은 스코어를 모으는게 목표인 게임입니다
___
# 기능
## 회원가입

___
## 로그인

___
## 랭킹 조회    

___
### 흐름
클라이언트
-> ASP.NET Core Middleware Pipeline
-> Controller
-> Service
-> Repository
-> EF Core DbContext
-> DB

___
# CI/CD

## 현재 적용
- 모든 PR과 기능 브랜치에서 .NET 10 빌드와 xUnit 테스트를 실행합니다.
- `develop` 반영 시 app/migrator 개발 이미지를 비공개 GHCR에 게시합니다.
- `release/**` 반영 시 GitHub Actions 안에서 PostgreSQL, EF migrator, app, Caddy를 임시 실행해 배포를 리허설합니다.
- `main` 반영 시 검증된 immutable image digest를 Tailscale SSH로 로컬 WSL 운영 환경에 배포합니다.
- Tailscale SSH가 GitHub Actions와 로컬 WSL을 연결하고, Caddy가 `wildlife-sports.duckdns.org`의 공개 HTTPS를 처리합니다.
- PostgreSQL과 ASP.NET 앱 포트는 Docker 내부에 유지하고 Caddy의 80/443만 외부에 공개합니다.
- 운영 마이그레이션 전 PostgreSQL custom-format dump를 검증하고 최근 7개를 유지합니다.
- 운영 구성과 초기 설정은 [배포 운영 가이드](deploy/README.md)를 참고합니다.
