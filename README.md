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
- GitHub Actions로 서버 빌드와 테스트를 자동 실행합니다.

## 추후 추가 예정
- 주요 기능 구현이 끝나고 배포 준비 단계에서 CD를 추가합니다.
- 배포 대상(Render, Railway, Azure, VPS/Docker 등)을 정한 뒤 환경 변수, DB 연결, 마이그레이션 적용 방식, 롤백 절차를 함께 정리합니다.
