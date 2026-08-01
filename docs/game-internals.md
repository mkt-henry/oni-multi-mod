# ONI 내부 구조 — 실측 기록

계획서 부록 A의 "미확인" 항목을 디컴파일로 확정한 기록이다.
추측이 아니라 `Assembly-CSharp.dll`을 `ilspycmd`로 디컴파일한 실제 코드에 근거한다.

- 대상: 게임 빌드 **744825**, Unity 6000.3.5f2
- 도구: `ilspycmd` 10.1.1.8388 (`dotnet tool install -g ilspycmd`)
- 확인 일시: 2026-08-01

```powershell
$env:DOTNET_ROLL_FORWARD="Major"
& "$env:USERPROFILE\.dotnet\tools\ilspycmd.exe" -t Immigration `
  "D:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed\Assembly-CSharp.dll"
```

---

## 1. Telepad (프린팅 팟)

```csharp
public class Telepad : StateMachineComponent<Telepad.StatesInstance>
{
    protected override void OnSpawn()   { ...; Components.Telepads.Add(this); }
    protected override void OnCleanUp() { Components.Telepads.Remove(this); }

    public void OnAcceptDelivery(ITelepadDeliverable delivery) { ... }
    public void AddNewBaseMinion(GameObject minion, bool extra_power_banks) { ... }
    public float GetTimeRemaining() { ... }
    public bool IsColonyLost() { ... }
}
```

### ★ 중요: ONI는 이미 팟 여러 개를 전제하고 있다

`Components.Telepads`는 **컬렉션**이고 `Telepad`이 스스로 등록/해제한다.
게임 코드 여러 곳이 이 목록을 순회한다. 팟을 하나 더 심는다고 해서 자료구조가 깨지지 않는다.

**계획서가 Phase 3의 최대 위험으로 본 "팟 단일 전제"는 실제보다 약하다.**
남은 문제는 팟의 *존재*가 아니라 팟에 딸린 *전역 상태*(아래 2번)다.

### 소유권 부착 지점

- `OnSpawn` → `OwnershipComponent` 부착 및 등록 (Phase 3)
- `OnAcceptDelivery` → 출력된 복제체에 팟 소유자 상속 (Phase 4)

### worldId 획득 방법 확인됨

```csharp
base.gameObject.GetComponent<KSelectable>().GetMyWorldId()
```

`OwnershipRecord.WorldId`를 런타임에 채울 수 있다. 계획서 §2.7(G6)의 전제가 성립한다.

---

## 2. Immigration (인쇄 주기) — G4의 실제 난이도

```csharp
public class Immigration : KMonoBehaviour, ISaveLoadable, ISim200ms, IPersonalPriorityManager
{
    public static Immigration Instance;         // 싱글톤

    [Serialize] public  float timeBeforeSpawn;  // 전역 타이머 1개
    [Serialize] private bool  bImmigrantAvailable;
    [Serialize] private int   spawnIdx;         // 누적 출력 횟수

    public float[] spawnInterval;               // spawnIdx로 인덱싱
    public int[]   spawnTable;

    public void Sim200ms(float dt)
    {
        if (!IsHalted() && !bImmigrantAvailable)
        {
            timeBeforeSpawn -= dt;
            if (timeBeforeSpawn <= 0f) bImmigrantAvailable = true;
        }
    }

    private bool IsHalted()
    {
        foreach (Telepad item in Components.Telepads.Items)
            if (item.GetComponent<Operational>()?.IsOperational == true) return false;
        return true;   // 팟이 하나도 가동 중이 아니면 정지
    }

    public int EndImmigration()
    {
        bImmigrantAvailable = false;
        spawnIdx++;
        timeBeforeSpawn = spawnInterval[Math.Min(spawnIdx, spawnInterval.Length - 1)];
        return spawnTable[Math.Min(spawnIdx, spawnInterval.Length - 1)];
    }
}
```

### 확정된 사실

| | |
|---|---|
| 싱글톤인가 | **그렇다** — `public static Immigration Instance` |
| 타이머 개수 | **1개** — `timeBeforeSpawn` 전역 |
| 팟별 상태 | **없음** — 팟은 `IsHalted()`의 게이트로만 쓰인다 |

### G4(팟별 독립 타이머)가 실제로 요구하는 것

단순히 타이머를 복제하는 문제가 아니다. **`spawnIdx`가 난이도 곡선을 결정한다.**

`spawnInterval[spawnIdx]`, `spawnTable[spawnIdx]` — 출력할수록 간격이 늘어난다.

- **공유 `spawnIdx`**: 2인이면 간격이 2배 속도로 증가 → 각자 체감 인쇄 속도가 절반
- **팟별 `spawnIdx`**: 각자 싱글플레이와 동일한 곡선 → 전체 인구는 2배

계획서 G4에서 "2인이면 인쇄 속도 2배 = 밸런스 문제"라고 적은 바로 그 지점이며,
**팟별 독립을 택했으므로 `spawnIdx`도 팟별로 분리해야 한다.**

### 구현 방향

`Immigration`의 `[Serialize]` 3개(`timeBeforeSpawn`, `bImmigrantAvailable`, `spawnIdx`)를
팟에 붙는 컴포넌트로 옮긴다. `Immigration.Instance`는 남겨두되(다른 코드가 참조한다)
타이머 진행과 `EndImmigration`을 팟 단위로 우회시킨다.

`Immigration`이 이미 `KMonoBehaviour` + `[Serialize]` 구조이므로,
`OwnershipComponent`와 동일한 패턴(컴포넌트에 상태를 얹고 세이브에 태우기)이 그대로 적용된다.

> 주의: `Sim200ms`는 `ISim200ms` 인터페이스 구현이다. Harmony로 가로챌 때
> 인터페이스 디스패치가 아니라 실제 메서드를 대상으로 잡아야 한다.

---

## 3. 템플릿 스탬프 — Phase 3b의 기반

계획서 §2.6(G5)에서 "월드젠 개조가 아니라 생성 후 템플릿 스탬프"를 택했는데,
그 방식이 **추측이 아니라 실제 public API로 존재함**을 확인했다.

### TemplateLoader.Stamp — 런타임 배치 진입점

```csharp
public static class TemplateLoader
{
    // ★ public static. 런타임에 임의 위치로 템플릿을 찍을 수 있다.
    public static void Stamp(TemplateContainer template, Vector2 rootLocation, System.Action on_complete_callback)
    {
        ActiveStamp item = new ActiveStamp(template, rootLocation, on_complete_callback);
        activeStamps.Add(item);
    }

    public static GameObject PlaceBuilding(Prefab prefab, int root_cell);
    public static GameObject PlacePickupables(Prefab prefab, int root_cell);
    public static GameObject PlaceOtherEntities(Prefab prefab, int root_cell);
    public static GameObject PlaceElementalOres(Prefab prefab, int root_cell);
    public static void PlaceUtilityConnection(GameObject spawned, Prefab bc, int root_cell);
    public static void ApplyGridProperties(int baseX, int baseY, TemplateContainer template);
}
```

**비동기 다단계 처리**다. `BuildPhase1`~`BuildPhase4`로 나뉘어 진행되고
(셀 → 건물 → 픽업 가능 항목 → 기타 엔티티/광석), 완료 시 콜백이 호출된다.
즉 스탬프 직후에 팟이 존재한다고 가정하면 안 되고, **`on_complete_callback` 안에서** 찾아야 한다.

### TemplateCache — 템플릿 로딩

```csharp
public static class TemplateCache
{
    private const string defaultAssetFolder = "bases";

    public static void Init();
    public static TemplateContainer GetTemplate(string templatePath);   // YAML 로드 + 캐시
    public static bool TemplateExists(string templatePath);
    public static string RewriteTemplateYaml(string scopePath);
}
```

`TemplateContainer`는 `cells`, `buildings`, `pickupables`, `elementalOres`,
`backwallEntities`, `otherEntities`와 `GetTemplateBounds(position, padding)`를 가진다.
**경계 계산이 내장돼 있으므로 배치 위치 겹침 검사에 그대로 쓸 수 있다.**

### 3b 구현 경로 (확정)

```
1. 월드가 쓴 시작 베이스 템플릿 경로를 얻는다
2. TemplateCache.GetTemplate(경로)        → TemplateContainer
3. GetTemplateBounds 로 후보 위치의 겹침·여유 검사
4. TemplateLoader.Stamp(template, 위치, onComplete)
5. onComplete 안에서 새 Telepad 을 찾아 2번째 플레이어에게 배정
   (TelepadOwnershipPatch.ResolveOwnerFor 를 이 정보로 확장)
```

---

## 4. 아직 확인하지 않은 것

- **`startingBaseTemplate` 을 선언한 타입.** 프로퍼티(`get_startingBaseTemplate`)로 존재하며
  `Assembly-CSharp-firstpass.dll`과 `Assembly-CSharp.dll` 양쪽에 심볼이 있다.
  `ilspycmd -t World` 로는 `ProcGen` 쪽 타입을 잡지 못했다 — 타입명 재확인 필요.
  하드코딩(`"bases/sandstoneBase"`) 대신 월드 정의에서 읽어야 바이옴별로 올바른 시작 구역이 나온다.
- **배치 위치 선정 기준** — 팟 간 최소 거리, 지형 적합성, 기존 구조물 회피
- **스탬프를 실행할 타이밍** — 월드 생성 완료 후 / 게임 시작 전 어느 훅인지
- **시작 구역 자원 보장** — 템플릿에 포함되는지, 별도 로직인지

**위 4개가 확정되기 전에는 3b를 구현하지 않는다.**

---

## 4. 계획서에 반영할 사항

| 항목 | 변경 |
|---|---|
| Phase 3 위험도 | **하향** — `Components.Telepads`가 이미 복수형이라 팟 추가 자체는 안전 |
| Phase 4 (G4) 난이도 | **상향** — 타이머뿐 아니라 `spawnIdx` 난이도 곡선까지 팟별 분리 필요 |
| 부록 A worldId | **확정** — `GetMyWorldId()`로 획득 가능 |
| 부록 A `Immigration` 싱글톤 | **확정** — `public static Immigration Instance` |
