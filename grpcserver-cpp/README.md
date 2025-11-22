# gRPC 기반 고성능 게임 서버

## 개요
3만명 이상의 동시 접속을 지원하는 고성능 MMO 게임 서버 구현을 전제로...

### 주요 특징
- **비동기 gRPC** - Completion Queue 기반 고성능 I/O
- **멀티스레드 아키텍처** - CQ 스레드 풀 + 워커 스레드 풀
- **Lock-free 설계** - Atomic 연산 및 shared_mutex 활용
- **세션 관리** - 30,000+ 동시 접속 지원
- **타임아웃 관리** - 자동 세션 정리
- **크로스 플랫폼** - Windows & Linux 지원

## 아키텍처

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ gRPC
       ▼
┌─────────────────────────────────┐
│     Completion Queue Pool       │
│  (I/O 스레드: CPU 코어 수)      │
└───────────┬─────────────────────┘
            │
            ▼
┌─────────────────────────────────┐
│      Worker Thread Pool         │
│  (비즈니스 로직: CPU 코어 x2)   │
└───────────┬─────────────────────┘
            │
            ▼
┌─────────────────────────────────┐
│      Session Manager            │
│  (Lock-free + Shared Mutex)     │
└─────────────────────────────────┘
```

## 빌드 요구사항

### 필수 패키지
- CMake 3.15+
- C++17 지원 컴파일러
- gRPC 1.50+
- Protobuf 3.20+

### Windows (Visual Studio)

#### 1. vcpkg로 의존성 설치
```powershell
# vcpkg 설치
git clone https://github.com/Microsoft/vcpkg.git
cd vcpkg
.\bootstrap-vcpkg.bat

# gRPC 및 protobuf 설치 (x64)
.\vcpkg install grpc:x64-windows
.\vcpkg install protobuf:x64-windows

# Visual Studio 통합
.\vcpkg integrate install
```

#### 2. CMake 빌드
```powershell
# 스크립트 빌드 (권장)
.\build.ps1 Debug rebuild

# CMake
mkdir build
cd build

cmake .. -DCMAKE_TOOLCHAIN_FILE=[vcpkg root]/scripts/buildsystems/vcpkg.cmake -A x64

# Visual Studio로 빌드
cmake --build . --config Debug
cmake --build . --config Release
```

#### 3. Visual Studio에서 직접 빌드
- `game.proto` 파일을 먼저 protoc로 컴파일
- 프로젝트 설정에서 vcpkg 경로 추가
- 빌드 구성을 Release로 설정하고 빌드

### Linux (Ubuntu/Debian)

#### 1. 의존성 설치
```bash
# 기본 도구
sudo apt update
sudo apt install -y build-essential autoconf libtool pkg-config

# gRPC 및 protobuf 설치 (소스에서 빌드 권장)
sudo apt install -y cmake git

# gRPC 빌드
git clone --recurse-submodules -b v1.50.0 https://github.com/grpc/grpc
cd grpc
mkdir -p cmake/build
cd cmake/build
cmake -DgRPC_INSTALL=ON -DgRPC_BUILD_TESTS=OFF ../..
make -j$(nproc)
sudo make install
cd ../../..
```

#### 2. CMake 빌드
```bash
mkdir build
cd build
cmake ..
make -j$(nproc)
```

## 실행 방법

### 서버 실행

#### Windows
```powershell
cd build\Release
.\game_server.exe [address] [cq_threads] [worker_threads]

# 예시 (기본값 사용)
.\game_server.exe

# 예시 (커스텀 설정)
.\game_server.exe 0.0.0.0:50051 8 16
```

#### Linux
```bash
cd build
./game_server [address] [cq_threads] [worker_threads]

# 예시
./game_server 0.0.0.0:50051 8 16
```

**파라미터 설명:**
- `address`: 서버 주소 (기본: 0.0.0.0:50051)
- `cq_threads`: Completion Queue 스레드 수 (기본: CPU 코어 수)
- `worker_threads`: 워커 스레드 수 (기본: CPU 코어 수 x2)

### 클라이언트 테스트

#### 1. 단일 클라이언트 테스트
```bash
# Windows
.\test_client.exe localhost:50051 single

# Linux
./test_client localhost:50051 single
```

#### 2. 부하 테스트 (100명, 60초)
```bash
# Windows
.\test_client.exe localhost:50051 load 100 60

# Linux
./test_client localhost:50051 load 100 60
```

#### 3. 동시 접속 테스트 (1000명)
```bash
# Windows
.\test_client.exe localhost:50051 concurrent 1000

# Linux
./test_client localhost:50051 concurrent 1000
```

**파라미터:**
- 첫 번째: 서버 주소
- 두 번째: 테스트 타입 (single/load/concurrent)
- 세 번째: 클라이언트 수 (load, concurrent 전용)
- 네 번째: 지속 시간 초 (load 전용)

## 성능 튜닝

### 서버 설정
```cpp
// SessionManager.h
static constexpr size_t MAX_SESSIONS = 30000;  // 최대 동시 접속자

// GameServer.cpp - CQ 스레드 수
int num_cq_threads = std::thread::hardware_concurrency();  // 기본: CPU 코어 수

// GameServer.cpp - 워커 스레드 수
int num_workers = std::thread::hardware_concurrency() * 2;  // 기본: CPU 코어 x2
```

### 권장 설정 (서버 사양별)

#### 소형 (4 코어, 8GB RAM)
- CQ Threads: 4
- Worker Threads: 8
- Max Sessions: 5,000

#### 중형 (8 코어, 16GB RAM)
- CQ Threads: 8
- Worker Threads: 16
- Max Sessions: 15,000

#### 대형 (16 코어, 32GB RAM)
- CQ Threads: 16
- Worker Threads: 32
- Max Sessions: 30,000+

### Linux 커널 튜닝
```bash
# 최대 파일 디스크립터 수 증가
ulimit -n 65536

# TCP 설정 최적화
sudo sysctl -w net.core.somaxconn=4096
sudo sysctl -w net.ipv4.tcp_max_syn_backlog=4096
sudo sysctl -w net.ipv4.ip_local_port_range="1024 65535"
```

## 프로토콜

### 로그인
```protobuf
rpc Login(LoginRequest) returns (LoginResponse);

message LoginRequest {
  string username = 1;
  string password = 2;
  string client_version = 3;
  int64 timestamp = 4;
}
```

### 로그아웃
```protobuf
rpc Logout(LogoutRequest) returns (LogoutResponse);

message LogoutRequest {
  string session_token = 1;
  int64 user_id = 2;
  int64 timestamp = 3;
}
```

## 모니터링

서버 실행 중 10초마다 통계가 자동 출력됩니다:
```
=== Server Stats ===
Active Sessions: 1523 / 30000
Pending Tasks: 45
```

## 확장 가능성

### 1. 데이터베이스 통합
```cpp
// GameServiceImpl.h - HandleLogin()
// TODO: DB 인증 추가
bool AuthenticateUser(const std::string& username, const std::string& password) {
    // MySQL, PostgreSQL, MongoDB 등과 연동
    return db_->Query("SELECT * FROM users WHERE username=? AND password=?", 
                     username, password);
}
```

### 2. Redis 세션 저장소
```cpp
// SessionManager.h
// 분산 서버 환경에서 세션 공유
void SaveSessionToRedis(const Session& session) {
    redis_->Set("session:" + session.session_token, 
                SerializeSession(session));
}
```

### 3. 메시지 큐 통합
```cpp
// 게임 이벤트를 RabbitMQ, Kafka 등으로 전송
message_queue_->Publish("game.events", event_data);
```

### 4. 마이크로서비스 아키텍처
- 인증 서버 분리

## 트러블슈팅

### Windows에서 "Cannot find grpc++" 오류
```powershell
# vcpkg 재설치
.\vcpkg remove grpc:x64-windows
.\vcpkg install grpc:x64-windows
.\vcpkg integrate install
```

### Linux에서 "grpc++ not found" 오류
```bash
# PKG_CONFIG_PATH 설정
export PKG_CONFIG_PATH=/usr/local/lib/pkgconfig:$PKG_CONFIG_PATH
sudo ldconfig
```

### 세션이 서버 풀 상태
- `MAX_SESSIONS` 값을 늘리거나
- 타임아웃 시간을 줄여서 비활성 세션을 빠르게 정리

### 성능이 기대치보다 낮음
1. CQ 스레드와 워커 스레드 수를 조정
2. Linux에서 커널 파라미터 튜닝
3. 프로파일러로 병목 구간 확인 (Valgrind, perf)
