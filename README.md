# VN-Practice

Unity 6으로 만든 짧은 비주얼 노벨입니다. 시나리오를 JSON으로 쓰면 대사, 배경, 캐릭터, 음악, 선택지, 엔딩까지 알아서 돌아가는 작은 엔진을 만들어 보는 게 목표였고, 평일 하루 2시간씩 2주 일정으로 잡고 작업했습니다.

<p>
  <img src="Docs/screenshots/01-title.png" width="32%">
  <img src="Docs/screenshots/02-dialogue.png" width="32%">
  <img src="Docs/screenshots/03-choice.png" width="32%">
</p>
<p>
  <img src="Docs/screenshots/04-hallway.png" width="32%">
  <img src="Docs/screenshots/05-rooftop.png" width="32%">
  <img src="Docs/screenshots/06-ending.png" width="32%">
</p>

미나는 생성한 일러스트를 넣었고, 배경과 지후, 음악은 아직 임시로 만든 placeholder입니다. 같은 파일 이름으로 덮어쓰면 코드나 시나리오를 건드리지 않고 바꿀 수 있습니다.

## 게임

대학 축제 전날 저녁, 연극 동아리에서 주인공을 맡은 지후가 무대에 서는 게 무섭다고 털어놓습니다. 플레이어는 동아리 회장 "나"가 되어 강의실, 복도, 옥상에서 세 번 선택하고, 지후에게 마음을 열어 주는 선택을 두 번 이상 하면 좋은 엔딩이 나옵니다. 한 판은 4~6분 정도이고 엔딩은 두 개입니다.

클릭이나 `Space`, `Enter`로 다음 줄로 넘어갑니다. 글자가 나오는 중에 누르면 그 줄이 한 번에 나옵니다.

## 어떻게 돌아가나

시나리오는 명령을 나열한 JSON입니다. `say`, `bg`, `show`, `hide`, `bgm`, `sfx`, `choice`, `label`, `jump`, `set`, `if`, `end`가 있고, 실행기가 위에서부터 하나씩 실행합니다. 분기는 `label`과 `jump`로 만들고, 선택지에서 `trust+1`처럼 플래그를 올린 뒤 마지막에 `if`로 엔딩을 가릅니다.

```json
{ "type": "say", "speaker": "지후", "text": "하아... 또 틀렸어." },
{ "type": "choice", "options": [
    { "text": "같이 대사를 맞춰 보자고 한다", "target": "help1", "set": "trust+1" },
    { "text": "다들 긴장하니까 잘할 거라고 말한다", "target": "cheer1" } ] },
{ "type": "if", "name": "trust", "value": ">=2", "target": "ending_good" }
```

게임 흐름은 타이틀, 시나리오 재생, 엔딩 화면, 다시 타이틀 순서입니다. 판이 끝나면 배경, 캐릭터, 소리, 플래그를 전부 초기화해서 바로 다시 할 수 있습니다.

## 만들면서 막혔던 것들

`JsonUtility`가 명령마다 다른 클래스로 읽어 주지 못해서, 모든 명령을 `type` 문자열과 선택 필드를 가진 클래스 하나로 두었습니다. 대신 실행하기 전에 라벨 이름, 값 문법, 위치 이름 같은 걸 한 번 검사해서, 오타가 있으면 게임 도중이 아니라 시작할 때 알 수 있게 했습니다.

실행기가 대사창이나 오디오를 직접 만지지 않고 `IDialogueView`, `IStageView`, `IAudioView`, `IChoiceView`만 부릅니다. 덕분에 화면 없이 가짜 View를 끼워서 실행기를 테스트할 수 있고, 선택지의 모든 조합을 자동으로 돌려 보는 테스트도 만들 수 있었습니다.

엔딩 문구에서 일부 글자가 □로 깨진 적이 있습니다. TextMeshPro의 동적 글꼴 아틀라스가 1024x1024 한 장이라 한글이 100자쯤 들어가면 가득 차기 때문이었고, Multi Atlas를 켜서 해결했습니다.

이미지도 한 번 걸렸습니다. 이 프로젝트는 새 이미지를 자동으로 여러 조각으로 나누도록 되어 있어서, `Resources.Load<Sprite>`가 캐릭터 전체가 아니라 첫 조각(223x223)만 돌려줬습니다. 배경과 캐릭터 폴더는 항상 Single로 가져오도록 임포터 스크립트를 넣었습니다.

캔버스 스케일이 고정 픽셀이라 화면이 커지면 글자와 대사창이 상대적으로 작아지는 구조였습니다. 빌드하기 전에 이걸 발견하고 1280x720 기준 `Scale With Screen Size`로 바꿨고, 배경이 늘어나지 않게 16:9 창으로 고정했습니다.

## 폴더

```
Assets/
  Scripts/
    Data/        시나리오 JSON을 읽는 클래스
    Runner/      ScriptExecutor(명령 실행), FlagStore(플래그), I*View(화면 인터페이스)
    DialogueManager, StageManager, AudioManager, ChoiceManager, GameFlow
  Editor/        빌드, 이미지 임포트, 시나리오 검사 도구
  Tests/EditMode/
  Resources/     Scripts(story.json), Backgrounds, Characters, Audio
Docs/            포맷 설명, 시나리오 구조, 테스트 설명, 스크린샷
```

## 실행하기

Unity 6000.3.11f1에서 프로젝트를 열고 `Assets/Scenes/SampleScene`을 실행하면 됩니다.

- 테스트: `Window > General > Test Runner`의 EditMode에서 실행합니다.
- 시나리오 검사: 메뉴 `VN > Validate Scripts`가 없는 이미지나 소리 파일, 도달할 수 없는 대사, 엔딩 연결 오류를 찾아 줍니다.
- 빌드: 메뉴 `VN > Build Windows (64-bit)`를 실행하면 `Builds/VN-Practice/VN-Practice.exe`가 만들어집니다. 명령줄에서는 `Unity.exe -batchmode -quit -projectPath . -executeMethod BuildTool.BuildWindows`입니다. 창은 1280x720으로 고정돼 있습니다.

## 아쉬운 점

- 배경, 지후 이미지, 소리가 아직 임시입니다.
- 분량이 목표(8~10분)보다 짧습니다.
- 세이브/로드, 백로그, 오토/스킵은 일정 때문에 넣지 못했습니다.
- 16:9 창만 지원합니다.

더 자세한 내용은 [시나리오 포맷과 구조](Docs/script-format.md), [시나리오 분기](Docs/story.md), [테스트](Docs/testing.md)에 적어 두었습니다.
