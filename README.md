.NET Standard, .NET Framework를 사용하는 네트워크 서버 코드 모음입니다.

- 포트폴리오
- 기타 구현 참고용

  
Projects
- TcpServerStandard  => 닷넷 스탠다드 기반으로 비동기 tcp server의 기본적 틀을 구현한다.
- MyCommonNet => 닷넷 스탠다드에서 사용할 네트워크 공통 라이브러리
- Packet 구조
- ![packet 구조](https://github.com/lcrlim/myportfolio/assets/68598899/8384e376-a3c6-4991-b181-9f05b76384f5)
  - Length, Type은 고정 4바이트, Body는 동적이고 Json String (UTF8)로 구현되어 있다. (다른 직렬화 Rule을 적용할 경우 PacketParser를 상속받아 별도 클래스 구현하면 된다.)

  
- NetStandardUnitTest => 닷넷 스탠다드 단위 테스트 프로젝트
  - 간단한 테스트 케이스로 client와 server 사이에 ping / pong 을 주고 받는 테스트 케이스 구현
- ThrottleApiServer => Owin selfhosing 서버 API에 Throttling을 적용하기위해 오픈 소스를 사용하고, 데이터 저장소를 기본(Redis)이 아닌 MSSQL로 사용하도록 코드를 구현한다.
