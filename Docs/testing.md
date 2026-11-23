# 테스트와 검증 도구

## 자동 테스트 (EditMode)

`Assets/Tests/EditMode` — Unity Test Framework(NUnit) 테스트 141개. 화면 없이 실행기 로직과 시나리오를 검사하므로 몇 초 안에 끝난다.

**실행 방법**
- 에디터: `Window > General > Test Runner` → EditMode → Run All
- 명령줄(에디터가 열려 있을 때): `unity test run --mode editor`
- 명령줄(에디터가 닫혀 있을 때): `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`

| 파일 | 개수 | 확인하는 것 |
|---|---|---|
| `FlagStoreTests` | 57 | `set` 값 문법(켜기, 지정, 더하기), 선택지 `set` 문자열, `if` 조건 비교, 잘못된 입력 거부 |
| `ScriptExecutorTests` | 40 | 대사 순서, `jump`/`label`, `end`와 엔딩 이름, 선택지 분기와 `set`, 플래그 `if` 루프, 사전 검증 오류 17종, 오류로 중단되는 경우(없는 라벨, 중복 라벨, 무한 루프, 잘못된 선택) |
| `ScriptAnalyzerTests` | 25 | 분석기가 오류/경고를 제대로 찾는지 (없는 리소스, 도달 불가 코드, `end` 없는 경로, 빈 대사/긴 대사, 플래그, 엔딩 연결) |
| `StoryContentTests` | 16 | 실제 시나리오의 **모든 선택 조합**을 자동으로 돌려 본 결과 (아래 참고) |
| `ScriptValidatorMenuTests` | 3 | 검증 메뉴가 씬의 엔딩 정의까지 검사하는지 |

### 시나리오 내용 테스트

`StoryContentTests`는 `story`와 `sample`을 대상으로, 선택지가 몇 개든 가능한 **모든 조합**을 하나도 빠짐없이 실행한다 (`TestSupport.ExploreAllPaths`). 어떤 경로로 해도 다음이 지켜져야 한다.

- 오류 없이 끝나고 `end`의 엔딩 이름이 있다
- 끝났을 때 화면에 캐릭터가 남아 있지 않고 BGM이 꺼져 있다
- 쓰는 배경, 캐릭터, BGM, 효과음 파일이 실제로 있고, 비어 있는 자리를 `hide`하지 않는다
- 모든 대사가 어느 경로에선가 나온다
- 엔딩이 2개 이상 도달 가능하다

시나리오를 고치거나 늘려도 이 테스트는 그대로 동작한다. `Story_*` 테스트(엔딩 조건 `trust >= 2`, 8가지 조합이 4:4로 나뉨)만 `story.json`의 규칙에 맞춘 것이라, 엔딩 조건이나 선택지 개수를 일부러 바꿀 때 함께 고치면 된다.

### 테스트가 실제로 문제를 잡는지 확인한 결과 (변이 검사)

코드와 시나리오를 일부러 망가뜨려 보고 테스트가 실패하는지 확인했다. 8건 중 7건을 잡았고, 나머지 1건(대사 길이 기준값 100자를 바꾸는 것)은 테스트가 기준값 자체가 아니라 경계 동작을 확인하도록 만든 것이라 일부러 잡지 않는다.

| 망가뜨린 것 | 실패한 테스트 |
|---|---|
| `>=` 비교를 `>`로 | 6개 |
| `jump`가 이동하지 않음 | 11개 |
| 선택지 `set`을 적용하지 않음 | 6개 |
| 분석기의 배경 리소스 검사 무력화 | 1개 |
| 엔딩에서 캐릭터를 숨기지 않음 | 1개 |
| 엔딩 조건 `>=2`를 `>=3`으로 | 2개 |
| 시나리오의 배경 이름 오타 | 3개 |

## 시나리오 검증 도구

메뉴 **VN → Validate Scripts**: `Assets/Resources/Scripts`의 모든 시나리오를 검사해 콘솔에 보여준다. 항목을 클릭하면 해당 JSON이 선택된다.

명령줄: `Unity.exe -batchmode -quit -projectPath . -executeMethod ScriptValidatorMenu.ValidateAllForBatch` (오류가 있으면 종료 코드 1)

| 구분 | 검사 항목 |
|---|---|
| 오류 | 구조 오류(없는 라벨, 잘못된 값 등 `ScriptExecutor.Validate`가 잡는 것), 없는 배경/캐릭터/BGM/효과음 파일, 엔딩 화면(`GameFlow.endings`)에 정의되지 않은 `end` 이름 |
| 경고 | 도달할 수 없는 명령, `end` 없이 끝나는 경로, 빈 대사, 100자가 넘는 대사, `if`가 읽는데 설정하는 곳이 없는 플래그, 설정만 하고 읽지 않는 플래그, 정의만 되고 쓰이지 않는 엔딩 |

시나리오를 고친 뒤에는 이 메뉴를 한 번 돌리면 오타를 미리 잡을 수 있다. 같은 분석기(`ScriptAnalyzer`)를 테스트도 쓴다.

## 어셈블리 구성

테스트가 코드를 참조할 수 있도록 어셈블리를 나눴다.

| 어셈블리 | 폴더 | 내용 |
|---|---|---|
| `VN.Runtime` | `Assets/Scripts` | 게임 코드 전체 (빌드에 포함) |
| `VN.Editor` | `Assets/Editor` | 임포터, 빌드 도구, 시나리오 분석기와 검증 메뉴 (에디터 전용) |
| `VN.Tests.EditMode` | `Assets/Tests/EditMode` | 테스트 (빌드에 포함되지 않음) |
