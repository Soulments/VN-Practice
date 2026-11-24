# 시나리오 포맷

시나리오는 `Assets/Resources/Scripts/`에 JSON 파일로 둔다. `commands` 배열을 위에서 아래로 하나씩 실행한다.

```json
{ "id": "sample", "commands": [ { "type": "say", "speaker": "지후", "text": "안녕" } ] }
```

`JsonUtility`는 명령마다 다른 클래스로 읽어 주지 못해서, 모든 명령이 `ScriptCommand` 클래스 하나를 쓰고 `type`으로 구분한다. 명령에 필요한 필드만 채우면 된다.

## 명령

| type | 필드 | 하는 일 |
|---|---|---|
| `say` | `speaker`, `text` | 대사를 보여준다. `speaker`가 비어 있으면 나레이션 |
| `label` | `name` | 점프할 위치 |
| `jump` | `target` | 라벨로 이동 |
| `if` | `name`, `value`, `target` | 조건이 참이면 라벨로 이동하고, 아니면 다음 명령으로 |
| `set` | `name`, `value` | 플래그 값을 바꾼다 |
| `choice` | `options` (`text`, `target`, `set`) | 선택지를 보여주고 고른 항목의 라벨로 이동 |
| `bg` | `name`, `duration` | 배경을 바꾼다 |
| `show` | `name`, `position`, `duration` | 캐릭터를 보여준다. `position`은 left, center, right이고 생략하면 center |
| `hide` | `name` 또는 `position`, `duration` | 캐릭터를 숨긴다 |
| `bgm` | `name`, `duration` | BGM을 재생한다. `name`이 `stop`이면 끈다 |
| `sfx` | `name` | 효과음을 한 번 재생한다 |
| `end` | `name` | 시나리오를 끝낸다. `name`은 엔딩 이름 |

`bg`, `show`, `bgm`, `sfx`의 `name`은 확장자 없는 리소스 이름이다. 파일은 UTF-8로 저장한다.

## 실행 규칙

실행하기 전에 `ScriptExecutor.Validate`가 시나리오 전체를 한 번 훑는다. 없는 라벨, 중복된 라벨, 빠진 필드, 해석할 수 없는 값이나 조건, left/center/right가 아닌 위치, 음수 `duration` 같은 문제가 있으면 로그에 오류를 남기고 시작하지 않는다. 오타가 게임 도중에 터지지 않게 하려는 것이다.

- 알 수 없는 `type`은 경고를 한 번 남기고 건너뛴다.
- 대사 없이 명령만 1만 개 넘게 이어지면(`label`과 `jump`가 서로를 가리키는 경우 등) 오류로 중단한다.
- 마지막 명령까지 가면 그냥 끝난다.
- 같은 파일 안에서 라벨 이름은 겹치면 안 된다.

## 배경과 캐릭터

배경은 `Resources/Backgrounds/{name}`, 캐릭터는 `Resources/Characters/{name}`에서 찾는다.

캐릭터가 서는 자리는 left, center, right 세 곳이고 자리마다 한 장씩 보인다. 표정은 이미지 이름으로 구분한다(`jihu_normal`, `jihu_smile`). 이미 캐릭터가 있는 자리에 다른 이미지를 `show`하면 `duration`의 절반 동안 사라진 뒤 나머지 절반 동안 새 이미지가 나타나고, 같은 이미지면 아무 일도 하지 않는다.

`duration`은 페이드 시간(초)이다. 0이거나 생략하면 즉시 바뀐다. 페이드가 끝나야 다음 명령으로 넘어간다. 이미지를 찾지 못하거나 비어 있는 자리를 `hide`하면 경고만 남기고 넘어간다.

이 프로젝트는 새 이미지를 자동으로 여러 조각으로 나눈다. 그러면 `Resources.Load<Sprite>`가 이미지 전체가 아니라 첫 조각만 돌려주기 때문에, `Editor/StageSpriteImporter.cs`가 두 폴더의 이미지를 항상 Single로 가져오게 한다. 이미지는 배경 1280x720, 캐릭터는 세로로 긴 투명 PNG가 맞다.

## 사운드

BGM은 `Resources/Audio/Bgm/{name}`, 효과음은 `Resources/Audio/Sfx/{name}`에 둔다(wav, ogg, mp3).

BGM은 한 번에 한 곡만 반복 재생한다. 이미 나오는 곡을 다시 요청하면 아무 일도 하지 않고, 다른 곡이면 이전 곡이 `duration`의 절반 동안 줄어든 뒤 새 곡이 나머지 절반 동안 커진다. 나오는 곡이 없으면 `duration` 전체에 걸쳐 커진다. `stop`은 `duration` 동안 줄어든 뒤 끈다. `duration`을 생략하면 즉시 바뀐다.

`bgm`과 `sfx`는 재생만 시작하고 기다리지 않는다. 볼륨은 씬의 `AudioManager`에서 조정한다.

## 선택지

```json
{ "type": "choice", "options": [
  { "text": "인사를 받아준다", "target": "kind" },
  { "text": "무시한다",       "target": "cold" } ] }
```

선택지 버튼은 화면 중앙 위쪽에 세로로 쌓이고, 직전 대사는 대사창에 그대로 남는다. 하나를 고르면 그 항목의 `target` 라벨로 이동한다. 분기가 다시 합쳐지는 지점은 `label`과 `jump`로 직접 만든다. 선택지는 몇 개든 쓸 수 있고 `choice`를 연달아 써도 된다.

고른 항목에 `set`이 있으면 이동하기 직전에 플래그를 바꾼다. 버튼 모양은 씬의 `ChoicePanel/ChoiceButtonTemplate`을 고치면 모든 선택지에 반영된다.

## 플래그

플래그는 이름이 있는 정수이고, 한 번도 쓰지 않은 이름은 0이다. 이름은 글자(한글 가능), 숫자, `_`로 쓰고 숫자로 시작할 수 없다. 공백은 무시한다.

`set` 명령의 `value`와 선택지의 `set`은 같은 문법이다.

- `"3"` 또는 `"=3"`: 3으로 지정
- `"+2"`, `"-1"`: 더하기, 빼기
- `value`를 생략하면 1로 켠다
- 음수로 지정하려면 `"=-3"`처럼 쓴다. `"-3"`은 3을 빼는 뜻이다.

선택지의 `set`은 이름과 값을 붙여서 쓴다: `"kindness+1"`, `"route=2"`, `"metMina"`.

`if`는 플래그를 `value`와 비교한다. `"2"`나 `"==2"`는 같다, `"!=2"`, `">2"`, `">=2"`, `"<2"`, `"<=2"`는 비교이고, `value`를 생략하면 0이 아닐 때 참이다.

엔딩은 `if`를 위에서부터 걸러 내리고 마지막에 `jump`로 기본 엔딩을 두는 식으로 가른다.

```json
{ "type": "label", "name": "judge" },
{ "type": "if", "name": "kindness", "value": ">=2", "target": "ending_good" },
{ "type": "jump", "target": "ending_normal" },
{ "type": "label", "name": "ending_good" },
{ "type": "say", "speaker": "", "text": "..." },
{ "type": "end", "name": "good" },
{ "type": "label", "name": "ending_normal" },
{ "type": "say", "speaker": "", "text": "..." },
{ "type": "end", "name": "normal" }
```

플래그는 `ScriptExecutor`를 새로 만들 때마다 처음부터 시작한다. 시나리오 맨 앞에 `set`으로 `=0`을 적어 두면 읽기 쉽다. 시나리오 밖에서 값이 필요하면 `ScriptRunner.Executor.Flags.Get("이름")`으로 읽는다.

## 게임 흐름

`GameFlow`가 타이틀, 시나리오 재생, 엔딩 화면, 다시 타이틀 순서를 맡는다. 한 씬 안에서 패널을 페이드로 바꾼다.

엔딩 화면의 제목과 설명은 `end`의 `name`과 같은 `id`를 가진 `GameFlow.endings` 항목(씬 인스펙터)에서 가져온다. `name`이 없으면 `THE END`, 정의되지 않은 이름이면 그 이름이 그대로 제목으로 나오고 경고가 뜬다.

판을 시작하거나 타이틀로 돌아올 때마다 배경, 캐릭터, 소리, 대사창을 모두 비운다. 시나리오가 오류로 중단되면 엔딩 화면 없이 로그를 남기고 타이틀로 돌아간다.

다른 시나리오를 쓰려면 `GameFlow.scriptName`을 바꾼다. 타이틀 없이 시나리오만 바로 돌려 보려면 `GameFlow`를 끄고 `ScriptRunner.playOnStart`를 켠다. `DialogueManager.autoAdvanceDelay`를 0보다 크게 하면 한 줄이 다 나온 뒤 입력 없이 자동으로 넘어가는데, 입력 없이 한 판을 돌려 볼 때 쓰려고 넣은 시험용 옵션이고 기본은 꺼져 있다.

## 글꼴

대사 글꼴(`NANUMSQUARE* SDF`)은 TextMeshPro의 Dynamic 아틀라스라서 처음 나온 글자를 그때그때 그려 넣는다. 1024x1024 한 장에는 한글이 100자 남짓밖에 안 들어가서 시나리오가 길어지면 글자가 □로 깨진다. 그래서 두 글꼴 에셋 모두 Multi Atlas Textures를 켜 두었고, 새 글꼴을 추가하면 같은 설정을 켜야 한다.

## 같이 보면 좋은 문서

[story.md](story.md)는 본편 시나리오의 분기 구조, [testing.md](testing.md)는 테스트와 시나리오 검사 도구에 대한 설명이다.
