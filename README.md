.NET Standard, .NET Framework를 사용하는 네트워크 서버 코드 모음입니다.

- 포트폴리오
- 기타 구현 참고용

  
Projects

1. TcpServerStandard
    - 닷넷 스탠다드 기반으로 비동기 tcp server의 기본적 틀을 구현한다.
    - MyCommonNet을 사용한 서버 콘솔 프로그램 구현
    - 패킷 파서 구현 클래스
  - MyCommonNet => 닷넷 스탠다드에서 사용할 네트워크 공통 라이브러리
    - 기본 패킷 클래스
    - 패킷 파서 인터페이스 : 필요한 패킷 구조에 따라 직렬화 클래스를 상속 구현하여 injection할 수 있는 구조 제공
    - TCP서버 객체 : TcpListener를 사용하여 TAP(작업 기반 비동기 패턴)로 구현, 비동기 처리로 고용량 처리를 목적으로 함
    - Client Connection 패킷 처리 Worker
    - 로깅은 Serilog 사용
  - Packet 구조
  - ![packet 구조](https://github.com/lcrlim/myportfolio/assets/68598899/8384e376-a3c6-4991-b181-9f05b76384f5)
    - Length, Type은 고정 4바이트, Body는 동적이고 Json String (UTF8)로 구현되어 있다. (다른 직렬화 Rule을 적용할 경우 PacketParser를 상속받아 별도 클래스 구현하면 된다.)

  
  - NetStandardUnitTest => 닷넷 스탠다드 단위 테스트 프로젝트
    - 간단한 테스트 케이스로 client와 server 사이에 ping / pong 을 주고 받는 테스트 케이스 구현
   
      
2. ThrottleApiServer
-   Owin Self-Hosing 서버 API에 Throttling을 적용하기위해 오픈 소스(WebApiThrottle)를 사용하고, 데이터 저장소를 기본(Redis)이 아닌 MSSQL로 사용하도록 코드를 구현한다.
    - 현재 상태에는 PerSecond or PerMinute or PerHour 등의 1가지 속성만 적용할 수 있으며, 여러 요소를 동시에 적용하기 위해서는 오픈소스 코드를 일부 수정하면 가능하다.
    - IP address 당 제한 / Endpoint 당 제한 / Client 식별자 당 제한 등을 사용할 수 있고, whilelist 등의 기능도 설정 할 수 있다.
    - 제한 설정은 Controller 전체에 또는 API당 제한 값을 다르게 설정할 수 있다.
