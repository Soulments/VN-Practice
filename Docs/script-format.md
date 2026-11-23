# 스크립트 데이터 포맷

시나리오는 `Assets/Resources/Scripts/{이름}.json` 에 둔다.
`ScriptLoader.Load("이름")` 으로 읽고, `commands` 를 위에서 아래로 실행한다.
`JsonUtility`는 다형성을 지원하지 않아서 모든 명령이 같은 `ScriptCommand` 클래스를 쓰고, `type` 으로 구분한다.

```json
{ "id": "sample", "commands": [ { "type": "say", "speaker": "클로드", "text": "안녕" } ] }
```

## 명령 목록

| type | 쓰는 필드 | 설명 | 구현 예정 |
|---|---|---|---|
| `say` | `speaker`, `text` | 대사 출력. `speaker`가 비면 나레이션 | 2~3일차 |
| `bg` | `name`, `duration` | 배경 교체 (페이드) | 4일차 |
| `show` | `name`, `position`, `duration` | 캐릭터 표시. position: left / center / right | 4일차 |
| `hide` | `name`, `duration` | 캐릭터 숨김 | 4일차 |
| `bgm` | `name` | BGM 재생 | 5일차 |
| `sfx` | `name` | 효과음 재생 | 5일차 |
| `choice` | `options[]` (`text`, `target`, `set`) | 선택지. 고르면 `target` 라벨로 이동, `set`이 있으면 플래그 갱신 | 6일차 |
| `label` | `name` | 점프 지점 | 6일차 |
| `jump` | `target` | 라벨로 무조건 이동 | 6일차 |
| `set` | `name`, `value` | 변수/플래그 설정 | 7일차 |
| `if` | `name`, `value`, `target` | 변수가 `value`면 `target` 라벨로 이동 | 7일차 |
| `end` | - | 스크립트 종료 (엔딩 화면 등으로 연결) | 8일차 |

> 일정 문서 기준 일차입니다. 2일차 `say`는 현재 `DialogueManager`의 하드코딩 대사를 데이터로 교체하는 작업과 맞물립니다.

## 규칙

- 라벨 이름은 한 파일 안에서 유일해야 한다. 중복이면 `BuildLabelIndex()`가 `FormatException`을 던진다.
- `name`에 쓰는 리소스 이름은 확장자 없이 쓴다. 실제 폴더 규칙은 4일차에 에셋을 넣을 때 정한다.
- 알 수 없는 `type`은 실행기에서 에러로 처리한다 (3일차).
- 파일 인코딩은 UTF-8.

## 샘플

`Assets/Resources/Scripts/sample.json` — 배경/BGM/캐릭터 연출, 대사 2줄, 선택지 2개와 두 갈래 분기, 종료까지 포함한다.
확인용으로 `ScriptLoadCheck` 컴포넌트를 씬 오브젝트에 붙이고 Play 하면 콘솔에 내용이 출력된다 (3일차 실행기가 생기면 삭제).
