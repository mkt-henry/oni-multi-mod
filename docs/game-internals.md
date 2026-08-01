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

---

## 9. Phase 4 실증 — 소유권 전달 경로 3종 (2026-08-01)

빌드 `2026-08-01 20:59 feature/duplicant-ownership@ab95c18`, 양쪽 PC 동일 확인.

호스트:
```
12:10:04.040 action=assign netId=-37802731 owner=76561198084204138 type=Duplicant actor=76561199073336502
```

클라이언트:
```
12:02:21.769 action=restore netId=1060217590   type=PrintingPod    <- 세이브 전송
12:02:22.846 action=restore netId=-1739327875  type=Duplicant      <- 세이브 전송
12:10:04.127 action=sync    netId=-37802731    type=Duplicant      <- 실시간 패킷
```

87ms 간격, netId·소유자 일치. **소유권 전달 경로 3종이 모두 검증됐다.**

| 경로 | 대상 | 수단 |
|---|---|---|
| `assign` | 호스트가 결정 | 로컬 |
| `restore` | 세션 시작 전부터 있던 것 | 세이브(로드/전송) |
| `sync` | 세션 중 생긴 것 | `OwnershipSyncPacket` |

### 부수 확인: 발신자 컨텍스트가 실사용됐다

호스트 로그의 `actor=76561199073336502` 는 **클라이언트가 인쇄를 지시했음**을 호스트가 기록한 것이다.
Phase 1 의 `PacketContext` 가 진단이 아닌 실제 기록에 쓰인 첫 사례이며, 권한 검사가 읽을 값이 바로 이것이다.

### upstream 버그: ScheduleAssignmentPacket

```
[GameClient] Failed to handle incoming packet: System.NullReferenceException
  at ONI_Together.Networking.Packets.Social.ScheduleAssignmentPacket.Apply()
```

복제체 인쇄 직후 발생. **우리가 소유권 패킷에서 대비한 것과 동일한 경합**이다 —
호스트가 새 복제체에 대한 패킷을 보냈는데 클라이언트에 아직 그 객체가 없다.
upstream 은 이 경우를 처리하지 않는다.

같은 순간 `OwnershipSyncPacket` 은 10ms 뒤 정상 처리됐다(`action=sync`).
`_pending` 대기열 설계가 실제로 필요했음이 확인된 셈이다.

전송 계층 `try/catch` 에 잡혀 크래시는 없으나 **클라이언트에서 새 복제체의 스케줄 배정이 실패**한다.
우리 기능과 무관한 선재 버그이므로 기록만 하고 손대지 않는다.
고칠 경우 처방은 동일하다: 객체가 없으면 버리지 말고 대기시켰다가 스폰 시 적용.

---

## 10. Phase 5 실증 — 권한 제어 (2026-08-01)

빌드 `feature/permission-service@d8c9c90`.

```
[StartingCrew] Claimed 10 previously unowned duplicant(s) for the nearest pod owner.

action=assign  x10  type=Duplicant  owner=76561198084204138
action=denied  command=move  x9     actor=76561199073336502  owner=76561198084204138
```

거부된 복제체 6종 중 **5종이 이번에 배정된 시작 크루**다. 예외 0건.

**계획서 Phase 5 완료 조건 대조**

| 조건 | 결과 |
|---|---|
| 자신의 복제체 명령 성공 | ✅ 호스트 정상 조작 |
| 다른 플레이어 복제체 명령 실패 | ✅ 거부 9건 |
| 호스트에서 권한 검증 | ✅ 패킷 수신 시점에서 거부 |
| 패킷 변조 시에도 호스트가 거부 | ✅ 구조상 성립 (클라 UI 무관) |
| 클라이언트 UI 비활성화 | ❌ 미구현 (Phase 8) |

### 두 번 겪은 같은 실수: 게이트를 잘못된 시점에 둔 것

1차 시도에서 `[StartingCrew]` 가 아예 안 찍혔다. 원인은 로그 옆줄에 있었다.

```
[Build] ... | local=76561198084204138 host=0 inSession=False
```

**`Game.OnSpawn` 시점에는 호스팅이 아직 성립하지 않았다.** 세이브 로드가 끝난 뒤에 설정된다.
거기서 `IsHostInSession` 으로 걸러버려 예약 자체가 안 걸렸다.

게이트는 *예약 시점*이 아니라 *실행 시점*에 있어야 한다. "세션 준비 완료"를 알리는 이벤트가 없어
제한 횟수 재시도로 처리했다.

> 이는 Phase 3a 에서 `TelepadOwnershipPatch` 가 복원을 관측할 수 없는 자리에서 보고하던 것과
> **같은 종류의 실수**다. 상태를 판단하는 코드가 그 상태를 볼 수 있는 시점에 있는지 먼저 확인할 것.

### 정책: 무소유 객체는 개방

레지스트리의 fail-closed 와 다른 개념이다.

- 레지스트리: 기록에 없으면 소유자라고 **주장하지 않는다**
- 권한: 아무도 주장하지 않은 것은 **보호하지 않는다**

시작 크루 배정이 들어오면서 복제체는 더 이상 이 경로에 도달하지 않는다. 백스톱으로만 남는다.

---

## 11. 3b 구현 경로 확정

### 런타임에 시작 베이스 템플릿을 얻는 법 (확인 완료)

```csharp
// WorldContainer.worldType 이 ProcGen 월드 경로다
WorldContainer wc  = ClusterManager.Instance.GetWorld(worldId);
ProcGen.World def  = ProcGen.SettingsCache.worlds.GetWorldData(wc.worldType);
string templatePath = def.startingBaseTemplate;          // 예: "bases/sandstoneBase"

TemplateContainer template = TemplateCache.GetTemplate(templatePath);
TemplateLoader.Stamp(template, position, onComplete);
```

- `ProcGen.SettingsCache.worlds` — `public static Worlds`
- `Worlds.GetWorldData(string)` / `HasWorld(string)` — 확인됨
- `WorldContainer.worldType` — `public string`

**하드코딩 불필요.** 월드 정의에서 읽으므로 바이옴에 맞는 시작 구역이 나온다.

### 건물 단독 배치는 불가능하다

완성 건물을 심는 API 자체는 있다 (모드의 `BuildingSyncer.SpawnBuilding` 이 이미 쓴다):

```csharp
BuildingDef def = Assets.GetBuildingDef("Headquarters");
def.Build(cell, Orientation.Neutral, null, def.DefaultElements(), 293.15f,
          "DEFAULT_FACADE", playsound: false, GameClock.Instance.GetTime());
// 위치 검증: def.IsValidPlaceLocation(visualizer, cell, orientation, out failReason)
```

**그러나 이것만으로는 안 된다.** 갓 생성된 월드는 시작 구역 말고는 전부 암반이라
`IsValidPlaceLocation` 을 통과하는 빈 공간이 존재하지 않는다.

`TemplateContainer` 는 `cells` 를 포함하고 `TemplateLoader` 의 `PlaceCells` 가 지형 자체를 찍는다.
즉 **템플릿 스탬프는 자기가 들어갈 공간을 스스로 파낸다.** 계획서 G5 의 선택이
"더 안전해서"가 아니라 **작동하는 유일한 방법**이었다.

### 남은 진짜 난제: 위치 선정

바닐라는 월드 그래프의 `WorldGenTags.StartLocation` 태그로 고르는데, 로드된 월드에는 그 그래프가 없다.
직접 판정해야 한다.

- 기존 팟에서 최소 거리
- `GetTemplateBounds(pos, padding)` 만큼의 여유
- 기존 건조물·시작 구역과 비겹침
- 월드 경계 및 우주 구간 회피

### 시작 자원

`TemplateContainer` 에 `pickupables`, `elementalOres`, `buildings` 가 있으므로
시작 자원과 팟이 모두 템플릿에 포함될 가능성이 높다. 실제 YAML 은 아직 확인하지 않았다.
스탬프 후 결과물로 확인하는 편이 빠르다.

---

## 12. Phase 3b 실증 — 플레이어별 시작 구역 (2026-08-01)

빌드 `feature/starting-area-placement@1af57ba`, `podtest` 월드.

```
[StartingArea] PlayerId(76561199073336502) has no printing pod; placing one.
[StartingArea] Stamping 'bases/sandstoneBase' at 90, 193
[StartingArea] Delivered 3 duplicants to PlayerId(76561199073336502)'s new pod.
[StartingArea] Stamp complete at 90, 193
```

소유권 최종 상태:

| 플레이어 | 팟 | 복제체 |
|---|---|---|
| 76561198084204138 (호스트) | 1 | 3 |
| 76561199073336502 (클라이언트) | 1 | 3 |

예외 0건. **기획의 핵심 — 한 소행성, 분리된 시작 위치, 각자의 팟과 복제체 — 이 성립했다.**

### 세 번의 시행착오와 각각의 교훈

**1. 배포하지 않고 테스트를 요청했다.** 코드는 커밋됐지만 게임에 올라간 적이 없었고,
dist 브랜치도 구버전이라 2번째 PC 역시 같은 구버전을 설치했다.
"기능이 안 된다"와 "구버전이 돌고 있다"는 화면상 구분되지 않는다.
→ dist 게시를 `deploy.ps1` 에 넣어 기억해야 하는 단계를 없앴다.

**2. `WorldContainer.worldType` 을 ProcGen 키로 가정했다.** 실제로는 번역 문자열 키였다.
`ProcGen.World` 는 `name`(STRINGS 키)과 `filePath`(실제 키)를 별도로 갖는다.
→ 실패 시 후보 목록을 로그에 남기도록 해서, 같은 정보 부족으로 두 번 추측하지 않게 했다.

**3. 스탬프는 지형을 만들 뿐 시야를 열지 않는다.** 팟은 생성됐고 소유권도 맞았는데 보이지 않았다.
바닐라에서 시작 구역이 보이는 것은 복제체가 거기 서 있어서 `GridVisibility` 가 주변을 밝히기
때문이지, 지형이 생겨서가 아니다. 스탬프한 구역에는 아무도 없다.
→ `GridVisibility.Reveal` + `Grid.Revealed` + `FogOfWarMask.ClearMask` 세 가지를 모두 처리.

### 클라이언트 복제는 하드 싱크 없이 해결했다

클라이언트의 월드는 접속 시 받은 세이브이므로, 이후 호스트가 만든 지형은 그쪽에 존재하지 않았다.
월드를 다시 보내는 대신 **같은 템플릿을 같은 좌표에 찍으라고 알렸다**.

- 템플릿은 양쪽이 이미 게임 파일로 보유
- 건물 NetId 는 좌표에서 파생 → 결과 일치
- 수 MB 대신 수십 바이트

소유권은 이 패킷에 담지 않는다. 호스트가 정하고 `OwnershipSyncPacket` 이 나른다.

### 시작 복제체는 템플릿에 없다

월드의 최초 복제체는 시작 베이스 템플릿이 아니라 게임 시작 로직이 스폰한다.
따라서 스탬프한 구역은 비어서 도착한다.

인쇄와 동일한 경로(`MinionStartingStats.Deliver`)로 팟을 스코프에 넣고 배달하면
소유권 상속과 클라이언트 전파가 **기존 기능만으로** 해결된다. 새로 만든 것이 없다.
