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

## 4. 바닐라의 시작 베이스 배치 경로

`ProcGen.World` (**`Assembly-CSharp-firstpass.dll`**, 네임스페이스 `ProcGen`):

```csharp
public class World : IHasDlcRestrictions
{
    public string startingBaseTemplate { get; set; }              // 예: "bases/sandstoneBase"
    public MinMax startingBasePositionHorizontal { get; private set; }  // 기본 (0.5, 0.5)
    public MinMax startingBasePositionVertical   { get; private set; }  // 기본 (0.5, 0.5)
}
```

`ProcGenGame.TemplateSpawning` (**`Assembly-CSharp.dll`**):

```csharp
private static void SpawnStartingTemplate(WorldGenSettings settings, List<TerrainCell> terrainCells, ...)
{
    // 시작 위치는 월드 그래프에서 태그로 지정된 노드다
    TerrainCell terrainCell = terrainCells.Find(tc => tc.node.tags.Contains(WorldGenTags.StartLocation));

    if (settings.world.startingBaseTemplate.IsNullOrWhiteSpace()) return;

    TemplateContainer template = TemplateCache.GetTemplate(settings.world.startingBaseTemplate);
    Vector2I position = new Vector2I((int)terrainCell.poly.Centroid().x, (int)terrainCell.poly.Centroid().y);
    RectInt templateBounds = template.GetTemplateBounds(position, s_poiPadding);

    if (IsPOIOverlappingBounds(placedPOIBounds, templateBounds)) { ... }
}
```

### 확정된 사실

| | |
|---|---|
| 템플릿 경로 출처 | `settings.world.startingBaseTemplate` — **하드코딩 불필요** |
| 위치 선정 | `WorldGenTags.StartLocation` 태그가 붙은 `TerrainCell`의 중심 |
| 겹침 검사 | `GetTemplateBounds(position, padding)` + `IsPOIOverlappingBounds` 가 이미 존재 |
| 위 경로의 실행 시점 | **월드 생성 중** (`WorldGenSettings`, `terrainCells` 가 살아있을 때) |

---

## 5. 3b 설계 결론

### 바닐라 경로는 재사용할 수 없다

`SpawnStartingTemplate`은 월드 생성 중에만 동작한다. `terrainCells`와 `WorldGenSettings`는
게임이 로드된 뒤에는 존재하지 않는다.

**그리고 이 모드의 호스팅은 언제나 이미 생성된 월드에서 시작한다** (§6 참조).
따라서 월드젠 후킹은 선택지가 아니며, 런타임 스탬프가 **유일하게 가능한 방법**이다.
계획서 §2.6(G5)이 택한 방향이 결과적으로 유일한 정답이었다.

### 구현 경로

```
1. 현재 월드의 startingBaseTemplate 경로를 얻는다
   (ProcGen.World — 로드된 월드에서 접근 경로 확인 필요)
2. TemplateCache.GetTemplate(경로)                → TemplateContainer
3. 2번째 시작 위치를 직접 고른다  ← 바닐라 로직 재사용 불가, 우리가 짜야 함
4. TemplateContainer.GetTemplateBounds(pos, pad)  → 겹침·여유 검사
5. TemplateLoader.Stamp(template, pos, onComplete)
6. onComplete 안에서 새 Telepad 을 찾아 배정
   (TelepadOwnershipPatch.ResolveOwnerFor 확장)
```

**3번이 3b의 실제 난제다.** 바닐라는 월드 그래프의 `StartLocation` 태그에 의존하는데,
로드된 월드에는 그 그래프가 없다. 직접 판정해야 한다.

- 기존 팟에서 최소 거리 이상
- 템플릿 footprint 만큼의 여유 공간
- 진공/우주 구간 회피
- 기존 건조물과 비겹침

### 남은 미확인 항목

- 로드된 월드에서 `ProcGen.World` (따라서 `startingBaseTemplate`) 에 접근하는 경로.
  `SettingsCache.worlds` 또는 `ClusterManager` → `WorldContainer` 경유로 추정되나 미확인.
- 시작 구역 자원이 템플릿에 포함되는지. `TemplateContainer`에 `pickupables`,
  `elementalOres` 필드가 있으므로 포함될 가능성이 높으나 실제 YAML 미확인.

---

## 6. 호스팅은 언제나 기존 세이브에서 시작한다

`ONI_Together/Menus/HostLobbyConfigScreen.cs`:

```csharp
MultiplayerSession.ShouldHostAfterLoad = true;
if (mainMenu.saveFileEntries.Count > 0)
    mainMenu.LoadGame();     // 세이브가 있으면 불러오기
else
    mainMenu.NewGame();      // 세이브가 0개일 때만 새 게임
```

upstream의 의도된 동작이다. 새 월드로 플레이하려면
**싱글플레이에서 생성·저장 → 그 세이브를 호스팅**하는 순서를 거쳐야 한다.

§5의 "월드젠 후킹 불가" 결론이 여기서 나온다.

---

## 7. Phase 3a 실증 결과 (2026-08-01)

게임 빌드 744825, 호스트 세션에서 확인.

```
[Ownership] action=register netId=1060217590 owner=76561198084204138 type=PrintingPod world=0 result=ok
```

세이브 파일(`클럽하우스.sav`) offset 16946 평문 영역:

```
ONI_Together.Networking.Ownership.OwnershipComponent
    OwnerId .... OwnedTypeRaw .... WorldId
```

**런타임에 `AddOrGet` 으로 붙인 컴포넌트가 KSerialization 직렬화 템플릿에 정상 등록된다.**
사전에 가장 우려했던 실패 모드는 발생하지 않았다.

- 인스턴스 값은 세이브의 압축 구간에 있어 정적 검사로는 읽을 수 없다
- **로드 후 복원 경로는 아직 미검증.** `[TelepadOwnership] Pod netId=... restored for ...`
  로그가 이미 코드에 있으므로, 해당 세이브를 호스트로 로드하면 자동으로 확인된다

---

## 4. 계획서에 반영할 사항

| 항목 | 변경 |
|---|---|
| Phase 3 위험도 | **하향** — `Components.Telepads`가 이미 복수형이라 팟 추가 자체는 안전 |
| Phase 4 (G4) 난이도 | **상향** — 타이머뿐 아니라 `spawnIdx` 난이도 곡선까지 팟별 분리 필요 |
| 부록 A worldId | **확정** — `GetMyWorldId()`로 획득 가능 |
| 부록 A `Immigration` 싱글톤 | **확정** — `public static Immigration Instance` |

---

## 8. 2인 세션 실증 (2026-08-01)

호스트 PC(`76561198084204138`) + 클라이언트 PC(`76561199073336502`), 게임 빌드 744825.
양쪽 모드 DLL SHA256 동일 확인 (`42104eb7...b10d`).

### 발신자 귀속 (Phase 1)

호스트 로그:
```
[PacketSender] First packet attributed to 76561199073336502 via GameStateRequestPacket.
               local=76561198084204138 host=76561198084204138 isHost=True
```

클라이언트 로그:
```
[PacketSender] First packet attributed to 76561198084204138 via GameStateRequestPacket.
               local=76561199073336502 host=76561198084204138 isHost=False
```

호스트는 클라이언트 패킷을 클라이언트에게, 클라이언트는 수신 패킷을 호스트에게 귀속시킨다.
`Unattributed` 경고 양쪽 0건. **권한 검사를 올릴 토대가 검증됐다.**

### 소유권 동기화 (Phase 3a)

세이브 전송이 실제로 일어났다:
```
[SaveFileRequest] Starting SECURE transfer of '클럽하우스2.sav' (1.54 MB) in 7 chunks
[SaveFileRequest] SECURE transfer complete.
```

클라이언트 로그:
```
[Ownership] action=register netId=1060217590 owner=76561198084204138 type=PrintingPod world=0 result=ok
```

**소유권 패킷을 한 개도 만들지 않았는데 클라이언트가 정확한 소유자와 동일한 netId를 갖는다.**
Phase 2에서 별도 메타데이터 대신 컴포넌트 직렬화를 택한 판단이 입증됐다.
계획서 Phase 3 완료 조건 "호스트와 클라이언트에서 동일한 ID 확인"도 함께 통과.

### 판정 기준의 결함 (기록)

클라이언트에서는 `[TelepadOwnership] ... restored` 가 찍히지 않았다.
컴포넌트 `OnSpawn` 순서 때문에 `Telepad.OnSpawn` 패치가 먼저 돌면서 `HasOwner=false` 로 관측했고,
클라이언트이므로 조용히 반환했다. 등록은 그 뒤 `OwnershipComponent.OnSpawn` 이 수행했다.

동작은 정상이나 **로그가 실제 상태를 반영하지 못한다.** `restored` 라인만 판정 기준으로 삼았다면
정상 동작을 실패로 오판했을 것이다. 로그를 상태와 일치시켜야 한다.

### 별건: upstream 경고

`[PacketSender] No connection found for SteamID 0` 244줄.
우리 코드가 아니라 upstream `PacketSender.SendToPlayer` 의 경고이며 접두사가 우연히 겹친다.
`Connection ... fully established` 가 3회 찍힌 것으로 보아 재접속이 있었고,
그 시점부터 주기적 동기화가 사라진 플레이어에게 계속 전송을 시도하는 것으로 보인다.
예외 0건이고 세션은 정상 동작하여 지금은 손대지 않는다. 진단 로그를 묻히게 하는 소음이므로 추후 처리.
