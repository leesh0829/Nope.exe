# Nope.exe

Windows용 규칙 기반 창 자동 닫기(또는 최소화/숨김/프로세스 종료) 유틸리티입니다. 특정 프로그램 하드코딩 없이 `rules.json` 설정으로 동작합니다.

## 주요 기능

- 주기적 최상위 창 탐색 (`EnumWindows`)
- 규칙 기반 매칭 (AND 결합, 빈 조건 무시)
  - `processNameExact`
  - `processPathExact`
  - `fileDescriptionContains`
  - `companyNameContains`
  - `windowTitleRegex`
  - `windowClassExact`
- 액션
  - `Close` (기본, `SendMessageTimeout` + `WM_CLOSE`)
  - `Minimize`
  - `Hide`
  - `KillProcess` (강제 종료)
- 안전장치
  - 창+규칙 단위 쿨다운 (`cooldownSeconds`)
  - 처리 로그 기록
- 로깅
  - `logs/app.log` 롤링 (기본 5MB, 최대 5개)
  - `--console`에서 표준출력 로그 동시 출력
- 실행 모드
  - 기본: 트레이 앱 (NotifyIcon)
  - `--console`: 콘솔 모드
  - `--headless`: 트레이 없이 백그라운드 모드
- 시작 프로그램 등록
  - `--install-startup` / `--uninstall-startup` (작업 스케줄러 사용)

## 빌드

```bash
dotnet build -c Release
```

## 실행

기본(트레이):

```bash
dotnet run -c Release
```

콘솔 모드:

```bash
dotnet run -c Release -- --console
```

헤드리스 모드:

```bash
dotnet run -c Release -- --headless
```

로그온 시 자동 시작 등록:

```bash
dotnet run -c Release -- --install-startup
```

자동 시작 등록 해제:

```bash
dotnet run -c Release -- --uninstall-startup
```

> 정책/환경에 따라 작업 스케줄러 명령은 관리자 권한이 필요할 수 있습니다.

## 트레이 메뉴

- 일시정지/재개
- 로그 보기
- 설정 열기 (내장 설정 UI, JSON 파일 저장)
- 종료

## 설정 편집

트레이 메뉴의 **설정 열기**를 누르면 규칙 편집 UI가 열립니다.

- 공통 설정: `pollIntervalMs`, `cooldownSeconds`
- 규칙 행별 편집: enabled, 조건(match), action, timeout
- 빠른 설정: 규칙 행 선택 후 **프로세스 파일 선택** 버튼으로 exe를 고르면 `processNameExact`/`processPathExact` 자동 입력
- **적용/저장** 시 현재 실행 중 설정에 반영되고 `rules.json`에 저장됩니다.
- 정규식이 잘못되면 저장 전에 오류를 표시합니다.

## 설정 파일 (`rules.json`)

실행 폴더 기준 `rules.json`을 사용하며, UI로 편집한 내용이 그대로 반영됩니다.

- 모든 조건은 AND
- 비어있는 조건은 무시
- 정규식은 .NET Regex (대소문자 무시)

예시는 저장소의 `rules.json`을 참고하세요.
