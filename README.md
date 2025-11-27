# High-Performance .NET Server Architecture Portfolio

![Platform](https://img.shields.io/badge/Platform-.NET%20Core-512BD4?style=flat-square&logo=dotnet)
![Language](https://img.shields.io/badge/Language-C%23%20%7C%20C%2B%2B-blue?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)
![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen?style=flat-square)

> **Enterprise-grade server application samples demonstrating high-concurrency TCP networking, scalable REST APIs, and gRPC microservices integration.**

---

## Overview

이 프로젝트는 **.NET Core**를 기반으로 서버 애플리케이션을 구현한 포트폴리오입니다.
단순한 기능 구현을 넘어, **비동기 네트워크 처리(TAP)**, **분산 시스템의 트래픽 제어(Rate Limiting)**, **표준 인증 프로토콜(OIDC)** 등 기술적 요구사항 구현을 추가하였습니다.

### Key Objectives
- **High Concurrency**: Task 기반 비동기 패턴(TAP)을 활용한 Non-blocking I/O 처리로 대규모 동시 접속 처리.
- **Microservice Ready**: gRPC 및 REST API를 통한 서비스 간 고속 통신 및 확장성 확보.
- **Reliability & Security**: API Throttling을 통한 과부하 방지 및 OIDC 기반의 인증 체계 구축.
- **Clean Architecture**: 네트워크 모듈의 추상화와 의존성 주입(DI)을 통한 유지보수성 향상.

---

## 기술 스택

| Category | Technology | Description |
| :--- | :--- | :--- |
| **Framework** | .NET (Core) 9.0+ | Server Application Runtime |
| **Language** | C#, C++ | Core Logic & Native Interop |
| **Protocol** | TCP/IP, gRPC, HTTP/1.1 | Async Socket, Protobuf, REST API |
| **Database** | MS SQL Server | Relational Data Storage |
| **Cache/Dist** | Redis | Distributed Rate Limiting & Caching |
| **Auth** | OIDC (OpenID Connect) | Custom Identity Provider Implementation |
| **Logging** | Serilog | Structured Logging |
| **Testing** | xUnit, Custom Tester | Unit Testing & Performance Benchmarking |

---

## 프로젝트 구조

```bash
lcrlim-myportfolio/
├── CommonNetwork         # [Core] 네트워크 공통 라이브러리 (Packet, Parser Interface)
├── TcpServerStandard     # [Server] Async TCP 서버 구현체 (TAP Pattern)
├── Web.Service           # [API] RESTful API 서비스 (RateLimit, OIDC, Swagger)
├── GrpcServer            # [gRPC] gRPC 서버 (C#)
├── GrpcClient            # [gRPC] gRPC 클라이언트
├── GrpcPerformanceTester # [Tool] gRPC 성능/부하 테스트 도구
├── grpcserver-cpp        # [gRPC] C++ 기반 gRPC 서버 (상호운용성 데모)
├── MyOpenId              # [Auth] OIDC 기반 인증 서버 구현
├── NetStandardUnitTest   # [Test] 단위 테스트 프로젝트
└── Aspire.*              # [Cloud] .NET Aspire 오케스트레이션 설정
```

---

## 시작하기
이 프로젝트를 로컬 환경에서 실행하기 위한 가이드입니다.

### 필수 요구사항
- .NET SDK 9.0 이상
- Visual Studio 2022 (또는 VS Code)
- SQL Server (Express 또는 Developer Edition)
- (Optional) Redis (분산 Rate Limiting 테스트용)

### 설치
- 레포지토리를 클론하고 솔루션을 빌드합니다.
``` bash
git clone [https://github.com/lcrlim/myportfolio.git](https://github.com/lcrlim/myportfolio.git)
cd myportfolio
dotnet restore
dotnet build
```
### 데이터베이스 설정
Web.Service 프로젝트 실행을 위해 데이터베이스 연결 문자열을 설정해야 합니다.

1. Web.Service/appsettings.json 파일을 엽니다.
2. ConnectionStrings 섹션의 DefaultConnection을 본인의 로컬 SQL Server 환경에 맞게 수정합니다.
```bash
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=MyPortfolioDB;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

---

### 실행 방법
A. Run Web Service (REST API)
  - API 서버를 실행하고 Swagger를 통해 테스트합니다.

```bash
cd Web.Service
dotnet run
```
  - Access: 브라우저에서 http://localhost:5000/swagger (포트는 설정에 따라 다름) 접속
  - Features: Token 발급, API Throttling 테스트 가능

B. Run TCP Server
  - 고성능 비동기 소켓 서버를 실행합니다.
```bash
cd TcpServerStandard
dotnet run
```
  - 서버가 시작되면 CommonNetwork에 정의된 포트(기본값)로 클라이언트 연결을 대기합니다.

C. Run gRPC Server & Tester
  - gRPC 통신 성능을 측정합니다.
1. Server 실행:
```bash
cd GrpcServer
dotnet run
```
2. Tester 실행 (새 터미널):
```bash
cd GrpcPerformanceTester
dotnet run
```
  - 테스트 결과로 초당 처리량(TPS)과 응답 지연 시간(Latency)이 콘솔에 출력됩니다.

---

### 테스트

Unit Testing
  - 핵심 네트워크 로직과 패킷 파싱에 대한 무결성을 검증합니다.
```bash
dotnet test NetStandardUnitTest
```

Performance Testing Results
- 자체 구현한 GrpcPerformanceTester를 통한 벤치마크 예시:
  - Note: 아래 수치는 개발 환경(Localhost) 기준입니다.

- Throughput: ~15,000 Requests/sec
- Average Latency: < 2ms

### Key Features in Detail
1. Async TCP Server Architecture
  - TAP (Task-based Asynchronous Pattern): TcpListener.AcceptTcpClientAsync와 NetworkStream.ReadAsync를 사용하여 스레드 블로킹 없는 I/O 처리를 구현했습니다.
  - Packet Separation: Header(Length, Type)와 Body(JSON)를 분리하여 처리하는 PacketParser를 전략 패턴으로 구현하여 확장성을 높였습니다.

2. Web Service Reliability
  - Rate Limiting: Microsoft.Extensions.RateLimiting을 사용하여 특정 IP나 User의 과도한 요청을 차단합니다. Redis를 연결하면 여러 서버 인스턴스 간에도 카운트를 공유할 수 있습니다.
  - OIDC Auth: OAuth 2.0/OIDC 표준을 준수하는 간이 인증 서버를 내장하여 보안 토큰 기반의 통신을 지원합니다.
