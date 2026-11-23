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
| `end` | - | 스크립트 종료. 엔딩 화면 연결은 8일차 | 종료만 구현 (3일차) |
| `bg` | `name`, `duration` | 배경 교체. 이전 배경 위에서 새 배경이 페이드인 | 구현됨 (4일차) |
| `show` | `name`, `position`, `duration` | 캐릭터 표시. position: left / center / right (생략하면 center) | 구현됨 (4일차) |
| `hide` | `name` 또는 `position`, `duration` | 캐릭터 숨김. `position`이 있으면 그 자리, 없으면 `name`이 표시 중인 자리 | 구현됨 (4일차) |
| `bgm` | `name`, `duration` | BGM 재생(반복). `name`이 `stop`이면 끈다 | 구현됨 (5일차) |
| `sfx` | `name` | 효과음 한 번 재생 | 구현됨 (5일차) |
| `choice` | `options[]` (`text`, `target`, `set`) | 선택지. 고르면 `target` 라벨로 이동, `set`이 있으면 플래그 갱신 | 6일차 |
| `set` | `name`, `value` | 변수/플래그 설정 | 7일차 |
| `if` | `name`, `value`, `target` | 변수가 `value`면 `target` 라벨로 이동 | 7일차 |

> 상태의 일차는 일정 문서 기준입니다. 새 명령을 구현하는 날 `ScriptExecutor.Run`의 `switch`에 case를 추가하면 됩니다.

## 실행 규칙

- 실행 전에 `ScriptExecutor.Validate`가 검사한다. `jump`/`if`/`choice`의 대상 라벨이 없거나, 라벨이 중복이거나, `choice`에 `options`가 없으면 에러를 로그로 남기고 **실행하지 않는다**.
- 아직 구현되지 않았거나 알 수 없는 `type`은 경고를 한 번 남기고 건너뛴다. (`choice`가 건너뛰어지는 동안은 선택 없이 다음 명령으로 흘러간다.)
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

## 구조

| 파일 | 역할 |
|---|---|
| `Data/ScriptCommand.cs`, `Data/ScriptData.cs` | JSON에 대응하는 데이터 클래스 |
| `Data/ScriptLoader.cs` | Resources에서 JSON을 읽어 `ScriptData`로 변환 |
| `Runner/ScriptExecutor.cs` | 명령 실행 로직 (순수 C#, 화면과 무관) |
| `Runner/IDialogueView.cs` | 실행기가 대사를 보여줄 때 쓰는 인터페이스 |
| `Runner/ScriptRunner.cs` | 씬 진입점. JSON을 읽어 실행기를 돌린다 |
| `DialogueManager.cs` | `IDialogueView` 구현. 타이핑 출력과 입력 대기 |
| `Runner/IStageView.cs` | 실행기가 배경/캐릭터 연출을 요청할 때 쓰는 인터페이스 |
| `StageManager.cs` | `IStageView` 구현. 배경 2장(교차 페이드)과 캐릭터 3자리를 Canvas의 Image로 표시 |
| `Editor/StageSpriteImporter.cs` | 배경/캐릭터 이미지를 Single 스프라이트로 고정 |
| `Runner/IAudioView.cs` | 실행기가 소리를 낼 때 쓰는 인터페이스 |
| `AudioManager.cs` | `IAudioView` 구현. BGM 1채널(곡 교체 페이드)과 효과음 재생 |

## 샘플

`Assets/Resources/Scripts/sample.json` — 배경/BGM/캐릭터 연출, 대사 2줄, 선택지 2개와 두 갈래 분기, 종료까지 포함한다.
배경 3장(classroom, hallway, rooftop), 캐릭터 3종(claude_normal, claude_smile, mina_normal), BGM 2곡(daily, evening), 효과음 2개(ding, pop)를 쓴다.
현재는 `choice`가 건너뛰어져서 `kind` 분기 → `jump end` 순으로 흐르고, `cold` 분기는 실행되지 않는다.
다른 시나리오를 시험하려면 `ScriptRunner`의 `scriptName`을 바꾸면 된다.
