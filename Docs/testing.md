# 테스트와 시나리오 검사

## 자동 테스트

`Assets/Tests/EditMode`에 Unity Test Framework(NUnit) 테스트가 140여 개 있다. 화면 없이 돌아서 몇 초면 끝난다.

- 에디터: `Window > General > Test Runner`의 EditMode에서 Run All
- 에디터가 열려 있을 때 명령줄: `unity test run --mode editor`
- 에디터가 닫혀 있을 때 명령줄: `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`

테스트는 크게 네 가지를 본다.

- `FlagStoreTests`: `set`의 값 문법(켜기, 지정, 더하기), 선택지 `set` 문자열, `if` 조건 비교, 잘못된 입력을 거부하는지
- `ScriptExecutorTests`: 대사 순서, `jump`와 `label`, `end`와 엔딩 이름, 선택지 분기, 플래그로 만든 루프, 실행 전 검사가 잡는 오류들, 오류로 중단되는 경우(없는 라벨, 중복 라벨, 무한 루프, 잘못된 선택)
- `ScriptAnalyzerTests`: 아래 시나리오 검사 도구가 문제를 제대로 찾는지
- `StoryContentTests`: 실제 시나리오가 어떤 선택을 해도 끝까지 잘 가는지

실행기는 화면 대신 `IDialogueView` 같은 인터페이스만 부르기 때문에, 테스트에서는 가짜 View(`TestSupport`의 `FakeGame`)를 끼워서 요청이 어떻게 들어오는지 기록한다.

### 시나리오 내용 테스트

`StoryContentTests`는 `story`와 `sample`에서 선택지가 몇 개든 가능한 모든 조합을 하나도 빠짐없이 실행한다(`TestSupport.ExploreAllPaths`). 처음에는 전부 첫 번째 선택지로 가 보고, 선택지가 나온 자리마다 다른 번호를 골라서 가지를 늘려 가는 방식이다. 어느 경로든 다음이 지켜져야 한다.

- 오류 없이 끝나고 `end`에 엔딩 이름이 있다.
- 끝났을 때 캐릭터가 화면에 남아 있지 않고 BGM이 꺼져 있다.
- 쓰는 이미지와 소리 파일이 실제로 있고, 비어 있는 자리를 `hide`하지 않는다.
- 모든 대사가 어느 경로에선가 나온다.
- 도달할 수 있는 엔딩이 두 개 이상이다.

시나리오를 고치거나 늘려도 이 테스트는 그대로 돌아간다. 다만 `Story_`로 시작하는 테스트는 본편의 규칙(`trust >= 2`면 좋은 엔딩, 8가지 조합이 4대 4)에 맞춘 것이라, 엔딩 조건이나 선택지 개수를 일부러 바꿀 때는 같이 고쳐야 한다.

테스트가 실제로 문제를 잡는지 확인하려고 코드와 시나리오를 일부러 망가뜨려 봤다(`>=`를 `>`로, `jump`가 이동하지 않게, 선택지 `set` 무시, 엔딩에서 캐릭터를 안 숨김, 배경 이름 오타 등). 8가지 중 7가지에서 테스트가 실패했고, 안 잡힌 하나는 대사 길이 기준값(100자) 자체를 바꾼 경우였다. 이 테스트는 기준값이 아니라 기준에 걸리는지를 확인하도록 써서 일부러 잡지 않는다.

## 시나리오 검사 도구

메뉴 `VN > Validate Scripts`는 `Assets/Resources/Scripts`의 모든 시나리오를 검사해서 결과를 콘솔에 보여 주고, 항목을 누르면 해당 JSON이 선택된다. 명령줄에서는 `Unity.exe -batchmode -quit -projectPath . -executeMethod ScriptValidatorMenu.ValidateAllForBatch`로 돌리고, 오류가 있으면 종료 코드 1을 돌려준다.

오류로 알리는 것:
- 실행 전 검사(`ScriptExecutor.Validate`)가 잡는 구조 오류
- 없는 배경, 캐릭터, BGM, 효과음 파일
- `GameFlow.endings`에 정의되지 않은 `end` 이름

경고로 알리는 것:
- 도달할 수 없는 명령
- `end` 없이 끝나는 경로
- 빈 대사, 100자가 넘는 대사
- `if`가 읽는데 어디서도 설정하지 않는 플래그, 설정만 하고 읽지 않는 플래그
- 정의만 되어 있고 쓰이지 않는 엔딩

시나리오를 고친 직후에 한 번 돌리면 오타를 게임을 실행하기 전에 찾을 수 있다. 테스트도 같은 분석기(`ScriptAnalyzer`)를 쓴다.

## 어셈블리

테스트가 게임 코드를 참조할 수 있게 어셈블리를 셋으로 나눴다.

- `VN.Runtime`: `Assets/Scripts`의 게임 코드. 빌드에 들어간다.
- `VN.Editor`: `Assets/Editor`의 임포터, 빌드 도구, 시나리오 검사 도구. 에디터에서만 쓴다.
- `VN.Tests.EditMode`: `Assets/Tests/EditMode`의 테스트. 빌드에는 들어가지 않는다.
