# Unity Project Rules

## Environment

1. Unity Editor가 프로젝트 실행과 에셋 관리의 기준이다.
2. C# 스크립트는 Visual Studio 2022 환경을 기준으로 작성한다.
3. Unity 버전과 현재 프로젝트의 API 호환성을 유지한다.
4. `.meta` 파일을 임의로 삭제하거나 재생성하지 않는다.
5. `Library`, `Temp`, `Logs`, `obj` 폴더는 직접 수정하지 않는다.

## C# and Unity conventions

1. 기존 프로젝트의 네이밍과 폴더 구조를 우선한다.
2. MonoBehaviour의 Unity 메시지 메서드 이름을 변경하지 않는다.
3. Inspector에 연결된 serialized field의 이름 변경은 신중하게 처리한다.
4. serialized field 이름을 변경해야 한다면 `FormerlySerializedAs` 사용 여부를 검토한다.
5. 불필요한 `Find`, `FindObjectOfType`, 매 프레임 `GetComponent` 호출을 추가하지 않는다.
6. `Update`에서 불필요한 할당을 만들지 않는다.
7. 코루틴, 이벤트 구독, 싱글톤 생명주기 문제를 확인한다.

## Validation

1. 수정 후 컴파일 오류 가능성을 확인한다.
2. Prefab, Scene, Inspector 참조가 깨질 가능성을 확인한다.
3. 여러 스크립트를 수정한 경우 호출 흐름과 의존성을 구조도로 보여준다.

## Persistent project context

1. Glasses 기획·Stage 로직 관련 작업을 재개할 때 [GLASSES_CONTEXT.md](C:/Users/gue20/Glasses/GLASSES_CONTEXT.md)를 먼저 읽는다. 이전 요약과 검증 기록의 출처는 [STAGE_WORK_CONTEXT.md](C:/Users/gue20/Glasses/STAGE_WORK_CONTEXT.md)다.
2. 맥락 문서의 사용자 확정 규칙, 현재 구현, 미해결 제안을 구분하고 최신 사용자 지시와 실제 코드를 다시 대조한다. 기록된 제안·할 일은 추가 수정이나 Git/Docs 작업의 자동 승인으로 취급하지 않는다.
3. 맥락 문서에 있는 역할·스크립트 수정 한도는 명시된 해당 task에만 적용한다. 다른 task의 역할을 임의로 바꾸지 않는다.
