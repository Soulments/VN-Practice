# VN-Practice

Unity 6으로 만든 짧은 비주얼 노벨입니다. 시나리오(JSON)를 읽어 대사, 배경, 캐릭터, 음악, 선택지, 플래그 분기, 엔딩을 처리하는 **스크립트 실행 엔진**을 직접 구현한 연습 프로젝트이고, 평일 하루 2시간씩 10일(2주) 일정으로 만들었습니다.

| 타이틀 | 대사 | 선택지 |
|:--:|:--:|:--:|
| ![타이틀](Docs/screenshots/01-title.png) | ![대사](Docs/screenshots/02-dialogue.png) | ![선택지](Docs/screenshots/03-choice.png) |
| **복도** | **옥상** | **엔딩** |
| ![복도](Docs/screenshots/04-hallway.png) | ![옥상](Docs/screenshots/05-rooftop.png) | ![엔딩](Docs/screenshots/06-ending.png) |

> 현재 배경, 캐릭터, 음악, 효과음은 모두 **임시(placeholder)** 입니다. 같은 파일 이름으로 덮어쓰면 코드와 시나리오를 고치지 않아도 교체됩니다 ([Docs/story.md](Docs/story.md)).

## 플레이 방법

- **다음 줄**: 마우스 왼쪽 클릭, `Space`, `Enter`. 글자가 나오는 중에 누르면 그 줄이 한 번에 다 나오고, 한 번 더 누르면 다음 줄로 넘어갑니다.
- **선택지**: 버튼을 클릭합니다.
- 타이틀에서 **시작하기**를 누르면 시작하고, 엔딩 화면에서 **타이틀로**를 누르면 처음으로 돌아가 다시 할 수 있습니다.
- 한 판은 약 4~6분이고, 엔딩은 2개입니다.

## 이야기

축제 전날 방과 후, 연극 주인공 역을 맡은 클로드가 무대 공포를 털어놓습니다. 플레이어는 반장인 "나"가 되어 교실, 복도, 옥상에서 세 번 선택합니다. 마음을 열어 주는 선택을 두 번 이상 하면 **엔딩 1(함께 선 무대)**, 아니면 **엔딩 2(엇갈린 박수)** 입니다. 자세한 분기는 [Docs/story.md](Docs/story.md)에 있습니다.

## 구현한 기능

| 기능 | 설명 |
|---|---|
| 스크립트 실행기 | JSON 명령(`say`, `label`, `jump`, `if`, `set`, `choice`, `end` 등)을 위에서 아래로 실행하고, 실행 전에 라벨 대상과 값을 검증 |
| 대사창 | 한 글자씩 타이핑 출력, 입력 시 즉시 완성, 화자 이름 표시 |
| 연출 | 배경 교차 페이드, 캐릭터 3자리(left/center/right) 표시/교체/숨김, 표정 교체 |
| 사운드 | BGM 1채널(곡 교체 시 페이드), 효과음 |
| 선택지와 분기 | 선택지 버튼, 라벨 점프 |
| 플래그와 엔딩 | 정수 플래그(`kindness+1`처럼 갱신), 조건 분기(`>=2` 등), 엔딩 이름으로 엔딩 화면 연결 |
| 게임 흐름 | 타이틀 → 시나리오 → 엔딩 화면 → 타이틀, 판마다 화면과 소리와 플래그 초기화 |

세이브/로드, 백로그, 오토/스킵은 일정에서 제외했습니다.

시나리오는 이런 JSON입니다 ([Docs/script-format.md](Docs/script-format.md)).

```json
{ "type": "say", "speaker": "클로드", "text": "하아... 또 틀렸어." },
{ "type": "choice", "options": [
    { "text": "같이 대사를 맞춰 보자고 한다", "target": "help1", "set": "trust+1" },
    { "text": "다들 긴장하니까 잘할 거라고 말한다", "target": "cheer1" } ] },
{ "type": "if", "name": "trust", "value": ">=2", "target": "ending_good" }
```

## 테스트와 검증

- **자동 테스트 141개** (EditMode, 몇 초): 에디터의 `Window > General > Test Runner`에서 실행하거나 `unity test run --mode editor`. 플래그 문법, 실행기 동작, 그리고 실제 시나리오의 **모든 선택 조합**이 오류 없이 끝나는지 확인합니다.
- **시나리오 검증 메뉴** `VN → Validate Scripts`: 없는 이미지/소리 파일, 도달할 수 없는 대사, 엔딩 연결 같은 문제를 시나리오를 고친 직후에 미리 찾아 줍니다.
- 자세한 내용은 [Docs/testing.md](Docs/testing.md)를 보세요.

## 구조

```
Assets/
  Scripts/
    Data/         ScriptData, ScriptCommand, ScriptLoader        (JSON 데이터와 로더)
    Runner/       ScriptExecutor, FlagStore, ScriptRunner, I*View (명령 실행과 화면 인터페이스)
    DialogueManager / StageManager / AudioManager / ChoiceManager  (화면과 소리 구현)
    GameFlow      타이틀 → 시나리오 → 엔딩 흐름
  Editor/         StageSpriteImporter, BuildTool, ScriptAnalyzer, ScriptValidatorMenu
  Tests/EditMode/ 자동 테스트 141개
  Resources/
    Scripts/      story.json (본편), sample.json (기능 시연용)
    Backgrounds/ Characters/ Audio/Bgm/ Audio/Sfx/
  Scenes/SampleScene.unity
Docs/             script-format.md (포맷과 구조), story.md (시나리오), testing.md (테스트와 검증), screenshots/
```

실행기는 화면을 직접 다루지 않고 `IDialogueView`, `IStageView`, `IAudioView`, `IChoiceView`에만 의존합니다. 그래서 화면 없이도 실행 로직을 시험할 수 있습니다.

## 개발 환경

- Unity 6 (6000.3.11f1), URP 2D 템플릿
- TextMeshPro (나눔스퀘어 글꼴, Dynamic + Multi Atlas), Input System, uGUI

## 빌드

에디터 메뉴 **VN → Build Windows (64-bit)** 를 실행하면 `Builds/VN-Practice/VN-Practice.exe`가 만들어집니다. 명령줄에서는 다음과 같이 실행합니다.

```powershell
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildTool.BuildWindows
```

- 창은 1280x720(16:9)으로 고정이고 전체화면 전환은 막아 두었습니다. UI는 `CanvasScaler`(1280x720 기준)로 배율이 맞춰지지만, 배경 이미지가 늘어나지 않도록 16:9만 지원합니다.
- `Builds/`는 `.gitignore`에 있어 저장소에 올라가지 않습니다. 빌드 폴더에 `*_BurstDebugInformation_DoNotShip` 폴더가 생기면 배포할 때 빼면 됩니다.

## 진행 상황

- [x] 기본 대화 시스템
- [x] 스크립트 데이터 포맷과 실행기 (say / label / jump / end)
- [x] 배경, 캐릭터 연출 (bg / show / hide, 페이드)
- [x] 사운드 (bgm / sfx, BGM 교체 페이드)
- [x] 선택지와 분기 (choice, 라벨 점프)
- [x] 플래그와 엔딩 판정 (set / if)
- [x] 타이틀/엔딩 화면, 타이틀부터 엔딩까지 한 판 흐름
- [x] 실제 시나리오 입력 (교실/복도/옥상, 선택지 3곳, 엔딩 2개)
- [x] 빌드 테스트와 정리 (Windows 64비트 빌드, 스크린샷, 문서)
- [x] 자동 테스트와 시나리오 검증 도구
