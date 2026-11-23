# 스크립트 데이터 포맷

시나리오는 `Assets/Resources/Scripts/{이름}.json` 에 둔다.
`ScriptRunner`가 `ScriptLoader.Load("이름")` 으로 읽고, `ScriptExecutor`가 `commands` 를 위에서 아래로 실행한다.
`JsonUtility`는 다형성을 지원하지 않아서 모든 명령이 같은 `ScriptCommand` 클래스를 쓰고, `type` 으로 구분한다.

```json
{ "id": "sample", "commands": [ { "type": "say", "speaker": "클로드", "text": "안녕" } ] }
```

## 명령 목록

| type | 쓰는 필드 | 설명 | 상태 |
|---|---|---|---|
| `say` | `speaker`, `text` | 대사 출력. `speaker`가 비면 나레이션 | 구현됨 (3일차) |
| `label` | `name` | 점프 지점 | 구현됨 (3일차) |
| `jump` | `target` | 라벨로 무조건 이동 | 구현됨 (3일차) |
| `end` | `name`(선택) | 스크립트 종료. `name`은 엔딩 이름이고 엔딩 화면이 이 이름으로 어떤 엔딩인지 찾는다 | 구현됨 (3일차, 엔딩 이름은 8일차) |
| `bg` | `name`, `duration` | 배경 교체. 이전 배경 위에서 새 배경이 페이드인 | 구현됨 (4일차) |
| `show` | `name`, `position`, `duration` | 캐릭터 표시. position: left / center / right (생략하면 center) | 구현됨 (4일차) |
| `hide` | `name` 또는 `position`, `duration` | 캐릭터 숨김. `position`이 있으면 그 자리, 없으면 `name`이 표시 중인 자리 | 구현됨 (4일차) |
| `bgm` | `name`, `duration` | BGM 재생(반복). `name`이 `stop`이면 끈다 | 구현됨 (5일차) |
| `sfx` | `name` | 효과음 한 번 재생 | 구현됨 (5일차) |
| `choice` | `options[]` (`text`, `target`, `set`) | 선택지. 고르면 `set`으로 플래그를 갱신하고 `target` 라벨로 이동 | 구현됨 (6~7일차) |
| `set` | `name`, `value` | 플래그/변수 값 바꾸기 | 구현됨 (7일차) |
| `if` | `name`, `value`, `target` | 조건이 참이면 `target` 라벨로 이동, 아니면 다음 명령 | 구현됨 (7일차) |

> 상태의 일차는 일정 문서 기준입니다. 새 명령을 구현하는 날 `ScriptExecutor.Run`의 `switch`에 case를 추가하면 됩니다.

## 실행 규칙

- 실행 전에 `ScriptExecutor.Validate`가 검사한다. `jump`/`if`/`choice`의 대상 라벨이 없거나, 라벨이 중복이거나, `choice`에 `options`가 없으면 에러를 로그로 남기고 **실행하지 않는다**.
- 아직 구현되지 않았거나 알 수 없는 `type`은 경고를 한 번 남기고 건너뛴다.
- 대사(`say`) 없이 명령만 10000개 이상 연속 실행되면(`label`↔`jump` 무한 루프 등) 에러를 남기고 중단한다.
- 마지막 명령까지 실행하면 그냥 끝난다. 명시적으로 끝내려면 `end`를 쓴다.
- 라벨 이름은 한 파일 안에서 유일해야 한다.
- `name`에 쓰는 리소스 이름은 확장자 없이 쓴다. 실제 폴더 규칙은 4일차에 에셋을 넣을 때 정한다.
- 파일 인코딩은 UTF-8.

## 배경/캐릭터 연출 (4일차)

- 이미지는 확장자 없이 `name`으로 가리킨다. 배경은 `Assets/Resources/Backgrounds/{name}.png`, 캐릭터는 `Assets/Resources/Characters/{name}.png`.
- 캐릭터는 left / center / right **세 자리**가 있고, 자리마다 한 장씩 표시된다.
  - 표정 차이는 이미지 이름으로 구분한다 (`claude_normal`, `claude_smile`). 같은 자리에 `show`하면 이전 이미지가 사라지고(`duration`의 절반) 새 이미지가 나타난다(나머지 절반).
  - 이미 같은 이미지가 그 자리에 있으면 아무 일도 하지 않는다.
- `duration`은 초 단위 페이드 시간이다. 0이거나 생략하면 즉시 바뀐다. 페이드가 끝나야 다음 명령으로 넘어간다.
- 이미지를 찾지 못하거나 표시 중이 아닌 캐릭터를 `hide`하면 경고만 남기고 넘어간다.
- 실행 전 검증: `bg`/`show`에 `name`이 없거나, `position`이 left/center/right가 아니거나, `duration`이 음수이거나, `hide`에 `name`과 `position`이 모두 없으면 실행하지 않는다.
- `Assets/Editor/StageSpriteImporter.cs`가 두 폴더의 이미지를 항상 **Single 스프라이트**로 임포트한다. 이 프로젝트의 기본 설정은 이미지를 자동으로 여러 조각으로 나누고, 그러면 `Resources.Load<Sprite>`가 첫 조각만 돌려주기 때문이다.
- 현재 들어있는 이미지는 임시(placeholder)다. 같은 파일명으로 덮어쓰면 코드와 시나리오 수정 없이 교체된다.
  권장 크기: 배경 1280x720(화면 비율 16:9), 캐릭터는 세로로 긴 투명 PNG (임시 이미지는 420x720).

## 사운드 (5일차)

- BGM은 `Assets/Resources/Audio/Bgm/{name}`, 효과음은 `Assets/Resources/Audio/Sfx/{name}`에 둔다. `name`은 확장자 없이 쓴다 (wav, ogg, mp3 모두 가능).
- BGM은 한 번에 한 곡만 나오고 반복 재생된다.
  - 이미 같은 곡이 나오고 있으면 아무 일도 하지 않는다.
  - 다른 곡이 나오고 있으면 이전 곡이 `duration`의 절반 동안 줄어든 뒤 새 곡이 나머지 절반 동안 커진다. 나오는 곡이 없으면 `duration` 전체로 커진다.
  - `name`을 `stop`으로 하면 `duration` 동안 줄어든 뒤 꺼진다.
  - `duration`을 생략하면 즉시 바뀐다.
- `bgm`과 `sfx`는 재생만 시작하고 기다리지 않는다. 바로 다음 명령이 실행된다.
- 소리 파일을 찾지 못하면 경고만 남기고 넘어간다.
- 볼륨은 씬의 `AudioManager` 컴포넌트에서 조정한다 (`bgmVolume` 0.6, `sfxVolume` 1).
- 실행 전 검증: `bgm`/`sfx`에 `name`이 없거나 `duration`이 음수이면 실행하지 않는다.
- 현재 들어있는 소리는 코드로 합성한 임시(placeholder) 파일이다 (`daily`, `evening`, `ding`, `pop`). 같은 파일명으로 덮어쓰면 교체된다.

## 선택지와 분기 (6일차)

```json
{ "type": "choice", "options": [
  { "text": "인사를 받아준다", "target": "kind" },
  { "text": "무시한다",       "target": "cold" } ] }
```

- `choice`를 만나면 선택지 버튼이 화면 중앙 위쪽에 세로로 나타나고, 하나를 고를 때까지 기다린다. 마지막 대사는 아래 대사창에 그대로 남아 있다.
- 고른 항목의 `target` 라벨로 이동해 거기서부터 계속 실행한다. 분기가 끝나 합쳐지는 지점은 `label`과 `jump`로 직접 만든다 (`sample.json`의 `kind` / `cold` → `end` 참고).
- 선택지는 몇 개든 쓸 수 있고(버튼이 가운데 정렬로 쌓인다), `choice`를 연달아 써도 된다.
- 실행 전 검증: `options`가 비었거나, 선택지의 `text`가 없거나, `target` 라벨이 없으면 실행하지 않는다.
- 선택지의 `set`은 고른 선택지에만 적용되고, 분기하기 직전에 플래그를 갱신한다. 문법은 아래 "플래그" 참고.
- `ChoiceManager`에 연결된 `ScriptRunner.choiceView`가 없으면 `choice`는 경고 후 건너뛰고, 그대로 다음 명령으로 흘러간다.
- 버튼 모양은 씬의 `ChoicePanel/ChoiceButtonTemplate`(견본)을 고치면 모든 선택지에 반영된다. 색은 견본의 Button 색상 설정, 글꼴·크기는 견본 아래 `Text`에서 바꾼다.

## 플래그와 엔딩 판정 (7일차)

플래그는 이름이 있는 **정수**이고, 한 번도 쓰지 않은 이름은 0이다. 선택으로 쌓은 값(호감도, 루트 표시 등)을 `if`로 읽어 엔딩을 가른다.
이름은 문자(한글 가능), 숫자, `_`로 쓰고 숫자로 시작할 수 없다 (`kindness`, `route2`, `호감도`). 공백은 무시된다.

**값 바꾸기** — `set` 명령의 `value`, 선택지의 `set`("이름" + 값)이 같은 문법을 쓴다.

| 쓰는 법 | 뜻 | 예 |
|---|---|---|
| `"value": "3"` 또는 `"=3"` | 3으로 지정 | `{ "type": "set", "name": "kindness", "value": "=0" }` |
| `"value": "+2"` / `"-1"` | 더하기 / 빼기 | `{ "type": "set", "name": "kindness", "value": "+2" }` |
| `value` 생략 | 1로 켜기 | `{ "type": "set", "name": "metMina" }` |
| 선택지 `"set": "kindness+1"` | 이름과 값을 한 덩어리로 | `"set": "kindness+1"`, `"set": "route=2"`, `"set": "metMina"` |

음수로 **지정**하려면 `"=-3"`처럼 `=`를 붙인다. (`"-3"`은 3을 빼는 뜻이다.)

**조건** — `if`의 `name`이 가리키는 플래그를 `value`와 비교해서, 참이면 `target` 라벨로 이동하고 거짓이면 다음 명령으로 넘어간다.

| `value` | 뜻 |
|---|---|
| `"2"` 또는 `"==2"` | 2와 같다 |
| `"!=2"`, `">2"`, `">=2"`, `"<2"`, `"<=2"` | 비교 |
| 생략 | 0이 아니면 참 (켜진 플래그인지 확인) |

**엔딩 판정 예** — `if`를 위에서부터 차례로 써서 조건을 걸러 내고, 마지막에 `jump`로 기본 엔딩을 둔다.

```json
{ "type": "label", "name": "judge" },
{ "type": "if", "name": "kindness", "value": ">=2", "target": "ending_good" },
{ "type": "jump", "target": "ending_normal" },
{ "type": "label", "name": "ending_good" },
{ "type": "say", "speaker": "", "text": "[엔딩 1] ..." },
{ "type": "jump", "target": "finish" },
{ "type": "label", "name": "ending_normal" },
{ "type": "say", "speaker": "", "text": "[엔딩 2] ..." },
{ "type": "label", "name": "finish" },
{ "type": "end" }
```

- 플래그는 `ScriptExecutor`를 만들 때마다 새로 시작한다. 처음부터 다시 하면 이전 값이 남지 않는다. 그래도 시나리오 맨 앞에 `set`으로 `=0` 초기화를 적어 두면 읽기 쉽다.
- 현재 값은 `ScriptRunner.Executor.Flags`로 읽는다 (`Get("kindness")`). 엔딩 화면 등 시나리오 밖에서 플래그가 필요할 때 쓴다.
- 실행 전 검증: `set`/`if`의 `name`이 올바르지 않거나, `set`의 `value`/`if`의 조건/선택지 `set`을 해석할 수 없거나, `if`의 `target` 라벨이 없으면 실행하지 않는다.
- 세이브/로드는 일정에서 제외되어 있어서 플래그를 저장하지 않는다. 필요해지면 `FlagStore.All`을 저장하고 `Set`으로 복원하면 된다.

## 게임 흐름: 타이틀과 엔딩 화면 (8일차)

`GameFlow`가 전체 흐름을 맡는다: **타이틀 → 시나리오 재생 → 엔딩 화면 → (타이틀로) 타이틀**. 한 씬(`SampleScene`) 안에서 패널을 페이드로 전환한다.

- **타이틀 화면** (`TitlePanel`): 제목, 부제, "시작하기"와 "종료" 버튼. 실행하면 가장 먼저 보이고, "종료"는 빌드에서는 앱을 끄고 에디터에서는 Play를 멈춘다.
- **엔딩 화면** (`EndingPanel`): 엔딩 제목, 설명, "타이틀로" 버튼. 시나리오가 끝나면 나타난다.
- **엔딩 이름 연결**: 시나리오의 `end` 명령 `name`이 `GameFlow.endings`의 `id`와 같은 항목을 찾아 제목과 설명을 채운다.
  ```json
  { "type": "end", "name": "good" }
  ```
  `GameFlow`의 `Endings`(씬 인스펙터)에서 `id`/`title`/`description`을 추가하거나 고친다 (현재 `good`=엔딩 1, `normal`=엔딩 2).
  `end`에 `name`이 없거나(`THE END`), `endings`에 없는 이름이면(이름이 그대로 제목, 경고) 기본 문구로 보인다.
- **새 판 시작과 타이틀 복귀**마다 이전 판의 배경, 캐릭터, BGM/효과음, 대사창을 모두 지운다 (`StageManager.ResetStage`, `AudioManager.StopAll`, `DialogueManager.Clear`). 플래그는 `ScriptExecutor`를 새로 만들어서 처음부터 시작한다.
- 시나리오를 실행하다 오류로 중단되면(`ScriptExecutor.Failed`: 검증 실패, 잘못된 선택, 무한 루프) 엔딩 화면을 보이지 않고 로그를 남기고 타이틀로 돌아간다.
- 다른 시나리오를 쓰려면 `GameFlow.scriptName`을 바꾼다. 타이틀 없이 시나리오만 바로 시험하려면 `GameFlow` 오브젝트를 끄고 `ScriptRunner.playOnStart`를 켠다.
- `DialogueManager.autoAdvanceDelay`를 0보다 크게 하면 한 줄이 다 나온 뒤 그 시간(초)이 지나면 입력 없이 다음 줄로 넘어간다. 기본은 0(끔)이고, 자동 시연이나 입력 없이 한 판을 돌려 볼 때 쓴다 (일정에서 뺀 "오토 모드"가 아니라 시험용 옵션이다).
- 현재 타이틀 배경(`Resources/Backgrounds/title.png`)은 임시 이미지다. 같은 이름으로 덮어쓰면 된다.

## 구조

| 파일 | 역할 |
|---|---|
| `Data/ScriptCommand.cs`, `Data/ScriptData.cs` | JSON에 대응하는 데이터 클래스 |
| `Data/ScriptLoader.cs` | Resources에서 JSON을 읽어 `ScriptData`로 변환 |
| `Runner/ScriptExecutor.cs` | 명령 실행 로직 (순수 C#, 화면과 무관) |
| `Runner/IDialogueView.cs` | 실행기가 대사를 보여줄 때 쓰는 인터페이스 |
| `Runner/ScriptRunner.cs` | JSON을 읽어 실행기를 돌린다. `Play(이름)`으로 재생하고 결과(엔딩 이름, 플래그, 실패 여부)는 `Executor`로 읽는다 |
| `GameFlow.cs` | 타이틀 → 시나리오 → 엔딩 화면 흐름과 판 초기화 |
| `DialogueManager.cs` | `IDialogueView` 구현. 타이핑 출력과 입력 대기 |
| `Runner/IStageView.cs` | 실행기가 배경/캐릭터 연출을 요청할 때 쓰는 인터페이스 |
| `StageManager.cs` | `IStageView` 구현. 배경 2장(교차 페이드)과 캐릭터 3자리를 Canvas의 Image로 표시 |
| `Editor/StageSpriteImporter.cs` | 배경/캐릭터 이미지를 Single 스프라이트로 고정 |
| `Runner/IAudioView.cs` | 실행기가 소리를 낼 때 쓰는 인터페이스 |
| `AudioManager.cs` | `IAudioView` 구현. BGM 1채널(곡 교체 페이드)과 효과음 재생 |
| `Runner/FlagStore.cs` | 플래그 저장소. `set` 문법과 `if` 조건을 해석하고 계산한다 (순수 C#) |
| `Runner/IChoiceView.cs` | 실행기가 선택지를 보여줄 때 쓰는 인터페이스 |
| `ChoiceManager.cs` | `IChoiceView` 구현. 견본 버튼을 복제해 선택지를 만들고 고른 번호를 돌려준다 |

## 글꼴 (참고)

대사 글꼴(`NANUMSQUARE* SDF`)은 TMP **Dynamic** 아틀라스다. 처음 나온 글자를 그때그때 아틀라스에 그려 넣는데, 1024x1024 한 장에는 한글이 100자 남짓밖에 들어가지 않아서 시나리오가 길어지면 □로 깨진다.
그래서 두 글꼴 에셋 모두 **Multi Atlas Textures**를 켜 두었다 (글자가 모자라면 아틀라스가 자동으로 늘어난다). 새 글꼴을 추가하면 같은 설정을 켜야 한다.
Play 모드를 돌리면 글꼴 에셋 파일이 바뀌는데(동적으로 추가된 글리프), 커밋하지 않아도 된다.

## 샘플

`Assets/Resources/Scripts/sample.json` — 배경/BGM/캐릭터 연출, 대사, 선택지 2번, 플래그 누적(`kindness`)과 엔딩 2개, 종료까지 포함한다.
선택은 복도(인사를 받아준다 `kindness+1`)와 옥상(솔직하게 마음을 전한다 `kindness+1`) 두 번이고, 합이 2 이상이면 엔딩 1(`end` name `good`), 아니면 엔딩 2(`normal`)다.
배경 3장(classroom, hallway, rooftop), 캐릭터 3종(claude_normal, claude_smile, mina_normal), BGM 2곡(daily, evening), 효과음 2개(ding, pop)를 쓴다.
복도에서 선택지가 나오고, "인사를 받아준다"는 `kind` 분기(효과음 `pop` + "고마워요!"), "무시한다"는 `cold` 분기("...그렇군요.")로 갈라진 뒤 `end`에서 합쳐진다.
다른 시나리오를 시험하려면 `ScriptRunner`의 `scriptName`을 바꾸면 된다.
