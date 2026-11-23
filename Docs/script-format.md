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
| `bg` | `name`, `duration` | 배경 교체 (페이드) | 4일차 |
| `show` | `name`, `position`, `duration` | 캐릭터 표시. position: left / center / right | 4일차 |
| `hide` | `name`, `duration` | 캐릭터 숨김 | 4일차 |
| `bgm` | `name` | BGM 재생 | 5일차 |
| `sfx` | `name` | 효과음 재생 | 5일차 |
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

## 구조

| 파일 | 역할 |
|---|---|
| `Data/ScriptCommand.cs`, `Data/ScriptData.cs` | JSON에 대응하는 데이터 클래스 |
| `Data/ScriptLoader.cs` | Resources에서 JSON을 읽어 `ScriptData`로 변환 |
| `Runner/ScriptExecutor.cs` | 명령 실행 로직 (순수 C#, 화면과 무관) |
| `Runner/IDialogueView.cs` | 실행기가 대사를 보여줄 때 쓰는 인터페이스 |
| `Runner/ScriptRunner.cs` | 씬 진입점. JSON을 읽어 실행기를 돌린다 |
| `DialogueManager.cs` | `IDialogueView` 구현. 타이핑 출력과 입력 대기 |

## 샘플

`Assets/Resources/Scripts/sample.json` — 배경/BGM/캐릭터 연출, 대사 2줄, 선택지 2개와 두 갈래 분기, 종료까지 포함한다.
현재는 `choice`가 건너뛰어져서 `kind` 분기 → `jump end` 순으로 흐르고, `cold` 분기는 실행되지 않는다.
다른 시나리오를 시험하려면 `ScriptRunner`의 `scriptName`을 바꾸면 된다.
