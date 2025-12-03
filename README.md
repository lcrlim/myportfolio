# .NET Core Server Portfolio

![Platform](https://img.shields.io/badge/Platform-.NET%20Core-512BD4?style=flat-square&logo=dotnet)
![Language](https://img.shields.io/badge/Language-C%23%20%7C%20C%2B%2B-blue?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)
![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen?style=flat-square)

> **Server application samples demonstrating high-concurrency TCP networking, scalable REST APIs, and gRPC microservices integration.**

---

## Overview

이 프로젝트는 **.NET Core**를 기반으로 
- TCP 소켓 서버 애플리케이션과 Rest API 서버를 구현한 포트폴리오입니다.
- TCP 소켓 서버는 **비동기 네트워크 처리(TAP)** 방식으로 패킷을 추가할 때 유지보수성을 높게 하기 위해 Reflection을 적용하여 Handler를 찾아 매핑을 구성합니다.
  - 패킷이 추가될 때 사용자는 아래의 2가지 행위만 구현합니다.
    - PacketType 및 패킷 클래스 정의
    - 패킷을 처리 로직이 구현된 Handler 클래스 구현
  - Client 객체를 ObjectPool로 구성해 사전 생성하고 재활용합니다.
  - PacketParser에서 Zero Allocation 방식을 적용해 GC 부담을 최소화
- Rest API 서버는 **분산 시스템의 트래픽 제어(Rate Limiting)**, **표준 인증 프로토콜(OIDC)** 등 기술적 요구사항 구현을 추가하였고, **Swagger**를 통한 문서를 제공합니다.

### 주요 목표
- TCP Server
  - **High Concurrency**: Task 기반 비동기 패턴(TAP)을 활용한 Non-blocking I/O 처리로 대규모 동시 접속 처리.
  - **Maintenance**: 패킷 추가시 Dispatcher를 수정하지 않고 추가되는 패킷의 처리 로직만 구현하면 되도록 구현.
- Rest API Server
  - **Reliability & Security**: API Throttling을 통한 과부하 방지 및 OIDC 기반의 인증 체계 구축.

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
├── MyCommonNet           # [Core] 네트워크 공통 라이브러리 (Packet, Parser Interface, Dispatcher)
├── TcpServerStandard     # [Server] Async TCP 서버 구현체 (TAP Pattern)
├── TestTcpClient         # [Client] TcpServerStandard를 테스트 하기 위한 테스트 클라이언트
├── NetStandardUnitTest   # [Test] 단위 테스트 프로젝트

├── Web.Service           # [API] RESTful API 서비스 (RateLimit, OIDC, Swagger)
├── MyOpenId              # [Auth] OIDC 기반 인증 서버 구현

├── GrpcServer            # [gRPC] gRPC 서버 (C#)
├── GrpcClient            # [gRPC] gRPC 클라이언트 (C#)
├── GrpcPerformanceTester # [Tool] gRPC 성능/부하 테스트 도구 (C#)

├── grpcserver-cpp        # [gRPC] C++ 기반 gRPC 서버
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
dotnet build .\myportfolio.sln
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

A. Run TCP Server
  - 비동기 TCP서버와 테스트 클라이언트를 실행합니다.  
```bash
cd TcpServerStandard
dotnet run

# 새 터미널:
cd TcpClientStandard
dotnet run
```
  - 서버가 시작되면 Program.cs에 고정된 포트(기본값:8888)로 클라이언트 연결을 대기합니다.
  - 테스트 클라이언트를 실행하면 127.0.0.1:8888 로 자동 연결합니다. help, ping, login, run 등을 테스트 할 수 있습니다.
  - 테스트 클라이언트에서 run을 입력하면 1000개의 연결을 연결하고 login -> ping 1000회 를 수행하고 모든 테스트가 완료될때까지 시간과 request/sec를 측정합니다.
  - run 10 을 입력하면 위 테스트를 10회 반복합니다.
  - 서버에서 2초마다 통계를 출력합니다. 통계 출력을 끄거나 켜려면 m 키를 토글해서 조정할 수 있습니다.
    
B. Run Web Service (REST API)
  - API 서버를 실행하고 Swagger를 통해 테스트합니다.
```bash
cd Web.Service
dotnet run
```
  - Access: 브라우저에서 http://localhost:5000/swagger (포트는 설정에 따라 다름) 접속
  - Features: Token 발급, API Throttling 테스트 가능

C. Run gRPC Server & Tester (C#)
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

D. Run gRPC Sever & Tester (C++)
  - gRPC를 구현한 C++ 서버를 실행하고 성능을 측정합니다.
1. Server 실행:
```bash
cd grpcserver-cpp
# cmake 설치 안되어 있으면 설치
winget install Kitware.CMake
# 또는
choco install cmake
# D:\work\Dev\vcpkg 경로에 vcpkg 설치되어 있다는 가정하에 아래 스크립트 실행
# 설치된 경로가 다름 build.ps1 파일 내부에 VcpkgPaths 수정
.\build.ps1

[SUCCESS] Build completed successfully!
[INFO] Verifying build output...
[SUCCESS] ✓ generated\game.pb.h
[SUCCESS] ✓ generated\game.pb.cc
[SUCCESS] ✓ generated\game.grpc.pb.h
[SUCCESS] ✓ generated\game.grpc.pb.cc
[SUCCESS] ✓ Release\game_server.exe
[SUCCESS] ✓ Release\test_client.exe

[SUCCESS] ==========================================
[SUCCESS] Build completed successfully!
[SUCCESS] ==========================================

[INFO] To run the server:
  cd build\Release
  .\game_server.exe

[INFO] To run the test client (in another terminal):
  cd build\Release
  .\test_client.exe localhost:50051 single
  .\test_client.exe localhost:50051 load 1000 60

# 서버 실행
.\game_server.exe
```
2. Tester 실행 (새 터미널):
```bash
.\test_client.exe localhost:50051 single
# 또는
.\test_client.exe localhost:50051 load 1000 60
```
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
