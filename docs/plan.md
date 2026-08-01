# Oxygen Not Included 분할 소유형 멀티플레이 모드 — 개발 계획 (개정판 r2)

> 이 문서는 루트의 `plan(8).md`(초판)를 대체한다.
> 초판은 코드베이스 실측 없이 작성되어 난이도 판단이 일부 반대로 되어 있었다.
> 개정 근거: (1) 게임 환경 실측, (2) 베이스 프로젝트 소스 분석, (3) 기획 결정 확정.

---

## 1. 콘셉트

> 하나의 소행성 안에서 각 플레이어가 **자신만의 프린팅 팟과 복제체**를 운영하고,
> 자원과 인프라를 연결해 공동 생존하는 **분할 소유형** 협동 멀티플레이 모드

기존 ONI 멀티플레이 모드(ONI Together 등)는 **하나의 식민지를 여러 명이 공동 조작**하는 구조다.
이 프로젝트는 **식민지 자체를 플레이어별로 분할**한다. 이것이 근본적 차이다.

| | 기존 모드 | 이 프로젝트 |
|---|---|---|
| 식민지 | 1개 공동 조작 | 플레이어 수만큼 분리 |
| 프린팅 팟 | 1개 | N개, 각자 소유 |
| 복제체 | 전원 공용 | 소유자만 직접 조작 |
| 시작 지점 | 동일 | 분리된 위치 |
| 연구 | 공유 | **플레이어별 분리** |
| 필요 계층 | 상태 동기화 | 상태 동기화 **+ 소유권/권한 + 콜로니 분할** |

**MVP 범위**: 하나의 큰 맵에서 서로 다른 위치에서 시작.
**장기 목표**: 서로 다른 행성(소행성)에서 시작 → Spaced Out 클러스터 기반.

---

## 2. 확정된 설계 결정

아래는 확정 사항이며, 변경 시 이 문서를 먼저 갱신한다.

### 2.1 일감 격리 (G1 개정) — 이 프로젝트의 핵심 규칙

> **개정 이력.** 초기 G1 은 "일감 풀은 공유"였고, 그 전제로 chore 격리를 난제 목록에서 뺐었다.
> 2026-08-01 에 뒤집혔다. **일감은 공유하지 않는다.** 굴착을 포함해 모든 지시가 플레이어별이다.
> chore 격리가 다시 이 프로젝트에서 가장 큰 작업이 된다.

**원칙: 일감은 그것이 작용하는 대상의 소유자에게 속하고, 그 소유자의 복제체만 수행한다.**

일감 종류마다 규칙을 따로 두지 않는다. 하나의 원칙에서 전부 파생시킨다.

| 대상 | 소유자 | 수행 가능 |
|---|---|---|
| 굴착 지시 | 지시한 플레이어 | 소유자의 복제체만 |
| 건설 지시 | 지시한 플레이어 | 소유자의 복제체만 |
| 청소·수확 등 지시 | 지시한 플레이어 | 소유자의 복제체만 |
| 건물 **사용** (냉장고에서 식사, 화장실, 연구, 요리, 발전) | 건물 소유자 | 소유자의 복제체만 |
| 자재 배달 | 목적지 건물의 소유자 | 소유자의 복제체만 |

### 예외 — 명시적으로 제한하지 않는 것

| 항목 | 이유 |
|---|---|
| **건물 해체** | 누구나 가능. 남의 건물도 부술 수 있다 |
| **배관 · 전선 · 환기 연결** | 서로 이을 수 있다. 계획서 3.3 "식민지 연결"의 전제 |
| **복제체 자신의 욕구** | 숨쉬기, 이동, 수면 등 지시가 아닌 행동. 제한하면 복제체가 죽는다 |

> **건물 사용 제한의 귀결:** 남의 냉장고에서 밥을 먹을 수 없고 남의 화장실을 쓸 수 없다.
> 각 플레이어는 자기 생활 시설을 갖춰야 한다. 의도된 설계다.
>
> 이는 §2.4 "자원 공유(G3)"와 층위가 다르다. 자원 *덩어리*는 공유되지만
> 그것이 **어느 저장고 안에 있는가**가 접근을 결정한다.
> 초반에 무소유 저장고가 많아 문제가 드러나지 않을 수 있으므로 플레이테스트에서 확인한다.

**배타성과 충돌**은 ONI 기본 동작을 따른다 — 한 타일을 두 복제체가 동시에 굴착할 수 없고,
예약 자리에 먼저 지어지면 기존 예약은 무산된다.

### 2.2 연구 분리 (G2, R1, R2)

- 연구 진행도와 테크 언락은 **플레이어별로 완전히 분리**한다.
- **연구 포인트는 연구 스테이션 건물의 소유자에게 귀속된다.** (R1 ①)
- 건물 언락 검사는 **건설 주문 시점**에 주문자 기준으로 수행한다.
- 언락하지 않은 건물은 **주문할 수 없다.** 타 플레이어가 대신 주문해 줄 수도 없다. (R2)

> R1과 R2는 2.1의 "건물 운용은 소유자 전용" 규칙에 의해 자동으로 정합해진다.
> 연구 스테이션은 소유자의 복제체만 돌릴 수 있으므로,
> "건물 소유자 기준"과 "작업한 복제체의 소유자 기준"이 항상 같은 결과가 된다. 구현은 전자로 단순화한다.

### 2.3 건물 소유자의 정의

**건물 소유자 = 건설 주문을 낸 플레이어.**
그리고 **주문자의 복제체만 그 주문을 시공한다.**

두 문장은 하나의 규칙이다. §2.1 개정으로 일감이 격리되었으므로 시공자는 언제나 주문자의 복제체이고,
따라서 "주문자 기준"과 "시공자 기준"이 같은 답을 낸다. 소유자가 비결정적으로 정해질 여지가 없다.

> 검토했다가 버린 대안: 남이 지어준 건물을 무소유(공용)로 두는 방식.
> 협동 건설이 자연스럽게 공용 인프라가 된다는 장점이 있으나,
> §2.1 이 일감 격리로 바뀌면서 애초에 그런 건물이 생기지 않는다.

### 2.4 자원 (G3)

- 굴착·채취한 자원은 **전 플레이어 공유**한다.
- `WorldInventory`(소행성 단위 자원 집계)는 **개조하지 않는다.**
- 초판 3.7의 개인/연합/공동 저장고 권한은 **MVP 비목표**로 내린다.

### 2.5 프린팅 팟 (G4)

- 플레이어당 팟 1개.
- **인쇄 주기 타이머는 팟마다 독립.** 전역 `Immigration` 타이머를 팟 단위로 분리한다.
- 협동 모드에서 팟은 파괴 불가.

### 2.6 시작 배치 (G5)

**월드젠 개조가 아니라, 생성 후 템플릿 스탬프 방식을 채택한다.**

ONI는 월드 생성 시 `TemplateLoader` / `TemplateContainer`로 시작 베이스 템플릿을 배치한다.
같은 메커니즘으로 N개를 찍는다. 월드젠 파이프라인을 포크하는 것보다 위험이 낮고 게임 업데이트에 잘 버틴다.

배치 조건:
- 팟 간 최소 이격 거리 확보
- 각 시작 구역이 독립 생존 가능 (산소원, 물, 초반 식량, 기본 금속, 굴착 가능 경로)
- 초기에는 서로 연결되지 않음 (중반에 굴착으로 연결)

### 2.7 다중 월드 대응 (G6)

MVP는 단일 소행성이지만, **소유권 레코드에 `worldId`를 처음부터 포함한다.**
`ClusterManager`, `WorldContainer`, `Clustercraft` 확인됨 — 다중 소행성 기반은 게임에 이미 존재한다.
나중에 붙이면 전면 재작업이 되므로 지금 넣는다.

### 2.8 플레이어별 분리 항목 (G7)

스킬, 모랄, 식단, 스케줄 **전부 분리**한다.

**단, 비용은 낮다.** 스킬(`MinionResume`), 식단(`ConsumableConsumer`), 모랄은 이미 복제체 단위이고,
스케줄도 `ScheduleManager`가 다중 스케줄을 지원한다.
"분리"는 시뮬레이션 개조가 아니라 **권한 게이트 + UI 필터링**으로 끝난다.

### 2.9 보류 (R3)

Spaced Out 데이터 뱅크 소비 주체 — 자원은 공유(G3)인데 연구는 분리(G2)라 결정이 필요하나,
DLC 단계 이슈이므로 MVP 이후로 보류한다.

### 2.10 비목표 (MVP)

PvP, 프린팅 팟 파괴, 공개 매치메이킹, Steam 외 플랫폼, 전용 서버 상시 운영,
DLC 지원, 호스트 마이그레이션, 플레이 도중 신규 참가, 자연 타일 개별 소유권,
완벽한 결정론적 시뮬레이션, 저장고 접근 권한 등급.

---

## 3. 실행 환경 (실측)

| 항목 | 값 |
|---|---|
| ONI 설치 | `D:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded` |
| 엔진 | **Unity 6000.3.5f2** (Unity 6.3), Mono BleedingEdge |
| Steam buildid | 24423041 (2026-07-31 갱신) |
| 번들 Harmony | `0Harmony.dll` 2.4.2 — 게임 내장, 별도 배포 불필요 |
| 설치된 DLC | `expansion1`, `dlc2`, `dlc3` |
| 모드 폴더 | `C:\Users\Administrator\Documents\Klei\OxygenNotIncluded\mods` |
| 개발 저장소 | `C:\dev\oni-multi` |

**중요**: ONI는 2026년 3월 Unity 6.3으로 엔진을 올렸고 다수 모드가 깨졌다.
Klei는 이후 모드 타겟을 **.NET Standard 2.1**로 권고한다. `net48` 타겟은 사용하지 않는다.

**운영 주의**: 개발 중 게임 자동 업데이트로 빌드가 깨질 수 있으므로 Steam에서 ONI 자동 업데이트를 끈다.

### 3.1 필수 도구 상태

| 도구 | 상태 |
|---|---|
| .NET SDK | ✅ **10.0.302** |
| .NET 런타임 | ✅ 8.0.24 / 10.0.3 / 10.0.10 |
| git | ✅ 2.55.0 — 단 PowerShell PATH에 없음 (MinVer가 요구) |
| Visual Studio | **불필요** — 전 프로젝트가 SDK 스타일, `dotnet build`로 충분 |
| ILSpy | ❌ 미설치 — 게임 내부 구조 확정에 필요 (Phase 7 전까지) |
| gh CLI | ❌ 미설치 — 필수 아님 |

빌드 절차와 함정 3가지는 **`docs/build-setup.md`** 참조. 검증 완료됨.

---

## 4. 베이스 프로젝트

### 4.1 선정

**`Lyraedan/Oxygen_Not_Included_Together`를 포크한다.**

초판은 `onimp/oni_multiplayer`를 1순위로 두었으나, 실측 결과 뒤집혔다.

| | onimp/oni_multiplayer | **Lyraedan/ONI_Together** |
|---|---|---|
| 마지막 실제 커밋 | 2024-08-26 (2026-03 push는 옛 커밋 rebase) | **2026-07-20** |
| 마지막 릴리스 | v1.1.3-alpha (2024-12) | **v0.7.3 (2026-06-17)** |
| TargetFramework | `net48` | **`netstandard2.1`** |
| Unity 6 대응 | 없음 | 대응 후 릴리스됨 |

**가져오는 것은 게임플레이가 아니라 배관이다** — Steam 로비, 패킷 직렬화, 세션 관리,
Harmony 부트스트랩, Unity 6 / netstandard2.1 빌드 세팅, 전용 서버, 타 모드용 공개 API.

규모: **489개 C# 파일 / 60,778줄**. 저자 자평("very early pre-pre alpha")보다 훨씬 성숙하다.

### 4.2 이미 구현되어 있어 재사용하는 것

| 초판 요구사항 | 실제 코드 |
|---|---|
| 4.6 `PlayerId` | `ONI_Together/Networking/MultiplayerPlayer.cs:8` — `ulong PlayerId` (Steam ID, LAN 폴백 포함) |
| 4.3 호스트 권위 구조 | `ONI_Together/Networking/MultiplayerSession.cs` — `IsHost`, `HostUserID`, `ConnectedPlayers` |
| 4.6 안정적 네트워크 ID | `ONI_Together/Networking/Components/NetworkIdentity.cs` — `[Serialize] int NetId` + `NetIdHelper.GetDeterministicBuildingId()` |
| 프린팅 팟 네트워킹 | `Networking/Packets/World/TelepadEntitySpawnPacket.cs`, `Packets/Social/ImmigrantSelectionPacket.cs`, `Patches/GamePatches/ImmigrantScreenPatch.cs` |
| 전송 계층 | `Networking/Transport/` — Steamworks / Riptide / 전용 서버 3종 추상화 |

**세이브에 직렬화되는 결정론적 객체 ID가 이미 존재한다.** 초판 Phase 2의 상당 부분이 불필요해진다.

### 4.3 없는 것 — 이 프로젝트의 1번 작업

**패킷 수신 시 발신자를 알 수 없다.**

```
Networking/Transport/Steamworks/SteamworksServer.cs:140   PacketHandler.HandleIncoming(bytes);
Networking/Transport/Riptide/RiptideServer.cs:197         PacketHandler.HandleIncoming(rawData);
Networking/Packets/Architecture/PacketHandler.cs:77       packet.OnDispatched();
Shared/Interfaces/Networking/IPacket.cs                   void OnDispatched();   // 발신자 파라미터 없음
```

전체 6만 줄에서 `PlayerId` 참조는 **19곳**뿐이며 대부분 로비/커서/채팅용이다.
"모두가 같은 식민지를 조작한다"는 전제로 설계되어 권한 검사 지점이 아예 없다.

**단, 발신자 정보는 이미 손에 있는데 버려지고 있다:**
- `SteamworksServer.cs:152` — 연결 이벤트에서 `m_identityRemote.GetSteamID()`를 이미 사용
- `Networking/Packets/Core/HostBroadcastPacket.cs:28` — 릴레이 봉투에 `public ulong SenderId`가 있으나
  `OnDispatched()`에서 내부 패킷에 전달하지 않고 버림

→ **배관만 뚫으면 된다. GO.**

---

## 5. 아키텍처

### 5.1 발신자 컨텍스트 (첫 구현 작업)

수신 경로에 발신자 ID를 흘려 앰비언트 컨텍스트로 노출한다.

```csharp
public static class PacketContext
{
    public static ulong CurrentSender { get; private set; }
    public static IDisposable Scope(ulong senderId);   // using 스코프로 설정/복원
}
```

적용 지점: `SteamworksServer.cs:140`, `RiptideServer.cs:197`, `PacketHandler.HandleIncoming`,
`HostBroadcastPacket.OnDispatched`, `BulkSenderPacket`, `DedicatedServerMessagePacket`.

**`IPacket.OnDispatched()` 시그니처는 변경하지 않는다.**
`ONI_Together_API`가 타 모드용 공개 API이며, 활발히 개발 중인 upstream과 영구 머지 충돌이 발생한다.

### 5.2 소유권 도메인 모델

```csharp
public readonly record struct PlayerId(ulong Value);

public enum OwnershipType
{
    PrintingPod, Duplicant, Building, Storage, Rocket, SharedProject
}

public sealed class OwnershipRecord
{
    public int NetId { get; init; }              // NetworkIdentity.NetId 재사용
    public int WorldId { get; init; }            // G6: 다중 소행성 대비, MVP에서는 단일 값
    public PlayerId Owner { get; init; }
    public OwnershipType Type { get; init; }
}
```

`NetworkObjectId`를 새로 만들지 않고 기존 `NetworkIdentity.NetId`를 그대로 쓴다.
이미 세이브에 직렬화되고 결정론적으로 생성된다.

```csharp
public interface IOwnershipRegistry
{
    bool TryGetOwner(int netId, out PlayerId owner);
    void Register(int netId, PlayerId owner, OwnershipType type, int worldId);
    void Transfer(int netId, PlayerId newOwner);
    void Remove(int netId);
    IEnumerable<OwnershipRecord> ByOwner(PlayerId owner);
}

public interface IPermissionService
{
    bool CanDirectControl(PlayerId actor, int duplicantNetId);   // 이동/스킬/스케줄/식단/이름
    bool CanConfigureBuilding(PlayerId actor, int buildingNetId);// 설정 변경/해체 지시
    bool CanOperateBuilding(int duplicantNetId, int buildingNetId); // 2.1 건물 운용 규칙
    bool CanOrderBuild(PlayerId actor, string buildingDefId);    // 2.2 언락 검사
    bool CanPrint(PlayerId actor, int podNetId);
}
```

### 5.3 권한 검사 위치

**호스트가 최종 권한을 가진다.** 클라이언트 UI 비활성화는 편의 기능일 뿐 보안 경계가 아니다.

```
클라이언트 입력 → 패킷 생성 → 호스트 수신
                                  ↓
                       PacketContext.CurrentSender 설정
                                  ↓
                       IPermissionService 검사
                          ├ 허용 → 실행 → 전 클라이언트 전파
                          └ 거부 → 요청자에게 거부 통보 + 구조화 로그
```

### 5.4 연구 분리 구조

`Research` 싱글톤을 플레이어별 인스턴스로 분리하고, 다음을 플레이어 단위로 필터링한다.

- 테크 진행도 / 언락 상태
- 건설 메뉴(`PlanScreen`) 노출 항목
- 연구 스테이션 운용 → 포인트 귀속 (R1)
- 건설 주문 시 언락 검사 (R2)

MVP 최대 난제이며, 별도 Phase로 분리한다.

---

## 6. 개발 단계 (개정)

초판 대비 변경: chore 격리 삭제, 연구 분리 승격, 소유권 모델 조기화.

| Phase | 내용 | 선행 조건 |
|---|---|---|
| ~~**0**~~ | ~~포크 · clone · 무수정 빌드 성공 · 모드 로드 확인~~ | ✅ **완료 (2026-08-01)** |
| **1** | 발신자 컨텍스트 배관 (5.1) — 게임 동작 변화 없음 | Phase 0 |
| **2** | 소유권 도메인 모델 + 레지스트리 + 세이브 직렬화 (5.2) | Phase 1 |
| **3** | 팟 N개 배치 (2.6 템플릿 스탬프) + 팟 소유권 등록 | Phase 2 |
| **4** | 복제체 소유권 상속 + 팟별 독립 인쇄 타이머 (2.5) | Phase 3 |
| **5** | 직접 조작 권한 게이트 (이동/스킬/우선순위/스케줄/식단/이름) | Phase 4 |
| **6** | 건물 소유권 + 건물 운용 규칙 (2.1, 2.3) | Phase 5 |
| **7** | **연구 분리** (5.4) — 최대 난제 | Phase 6 |
| **8** | 세이브 / 로드 / 재접속 / 하드 싱크 후 소유권 보존 | Phase 7 |
| **9** | 멀티플레이 UI — 소유자 표시, 색상 구분, 권한 거부 알림 | Phase 8 |
| **10** | 오프라인 관리 · 전멸 복구 · 장시간 안정화 | Phase 9 |

### Phase 0 완료 기록 (2026-08-01)

| 조건 | 결과 |
|---|---|
| 코드 무수정 상태에서 전체 솔루션 빌드 성공 | ✅ 오류 0 / 경고 14 |
| 모드 DLL 생성 및 모드 디렉터리 배포 | ✅ `ONI_Together.dll` 3,988 KB |
| ONI에서 모드 로드 | ✅ `Successfully loaded from path 'root' with content 'DLL'` |
| 멀티플레이 초기화 | ✅ `[SteamLobby] Callbacks registered.` |
| 치명적 오류 없음 | ✅ Exception 0 / Harmony 패치 실패 0 |

환경 사실:

- 게임 빌드 **744825** (release), Unity 6000.3.5f2
- 모드 `minimumSupportedBuild` 700386 → 744825 > 700386 이므로 버전 경고 없음
- 로그의 경고 6건은 전부 타 모드(`Customizable Speed` BOM 파싱, 한글 언어팩 translation) 발생분

> **첫 성공 기준은 기능 구현이 아니다.** 무수정 빌드 + 게임 내 정상 실행이다. — 달성.

### Phase 1 완료 조건

- 호스트가 임의 패킷의 발신자 `PlayerId`를 정확히 식별
- Steamworks / Riptide / 전용 서버 3경로 모두 동작
- 기존 멀티플레이 기능 회귀 없음
- 싱글플레이 동작 변화 없음

---

## 7. 남은 난제 우선순위

| 순위 | 항목 | 상태 |
|---|---|---|
| **1** | **chore 격리 (§2.1 개정)** | **미착수. G1 이 뒤집히며 되살아난 최대 작업** |
| 2 | 건물 소유권 (§2.3) | 미착수. chore 격리의 판정 근거이자 연구 분리의 전제 |
| 3 | 연구 분리 (G2) | 미착수. 관문은 `Tech.IsComplete()` 하나로 확인됨 |
| 4 | 팟별 독립 인쇄 타이머 (G4) | 미착수. `Immigration` 싱글톤 + `spawnIdx` 분리 |
| ~~5~~ | ~~발신자 컨텍스트 + 권한 계층~~ | ✅ Phase 1·5 완료 |
| ~~6~~ | ~~시작 지점 템플릿 스탬프 (G5)~~ | ✅ Phase 3b 완료 |

**2번이 1번보다 먼저다.** "이 일감을 수행해도 되는가"를 판정하려면 대상의 소유자를 알아야 하는데,
현재 소유권이 붙는 것은 프린팅 팟과 복제체뿐이다.

---

## 8. 위험 요소 (개정)

| 위험 | 영향 | 대응 |
|---|---|---|
| Unity 6 전환 후 모드 생태계 불안정 | 빌드/런타임 실패 | netstandard2.1 고정, Klei 권고 준수 |
| ONI 자동 업데이트 | 개발 중 갑작스러운 파손 | Steam 자동 업데이트 비활성화, 지원 buildid 기록 |
| upstream 활발한 개발 | 머지 충돌 누적 | `IPacket` 등 공개 API 시그니처 불변 유지, 신규 코드는 독립 모듈로 |
| 연구 분리 난이도 과소평가 | 일정 지연 | Phase 7로 격리, 실패 시 "연구 공유"로 폴백 가능하도록 설계 |
| **일감 격리로 복제체가 할 일을 못 찾음** | 유휴·생산성 붕괴 | 예외 목록(§2.1)을 좁게 유지. 플레이테스트에서 유휴율 관찰 |
| **남의 저장고에 갇힌 식량** | 아사 | §2.1 단서 참조. 필요 시 생존 자원 접근 예외를 검토 |
| **기획 규칙의 잦은 반전** | 구현 폐기·재작업 | 규칙 변경 시 코드보다 계획서를 먼저 고친다 (2026-08-01 G1 반전 시 적용) |
| 클라이언트 간 시뮬레이션 오차 | 상태 불일치 | 명령 동기화 + 주기적 하드 싱크 (베이스 구조 활용) |
| 권한 검사가 클라이언트에만 존재 | 우회 가능 | 호스트 최종 검증 (5.3) |
| 테스트 인스턴스 부족 | 검증 불가 | 2번째 Steam 계정 또는 2번째 PC 확보 필요 |

---

## 9. MVP 완료 기준

- 2명이 Steam으로 같은 게임에 접속한다.
- 서로 다른 시작 위치에 프린팅 팟이 하나씩 생성된다.
- 각 팟이 독립 타이머로 복제체를 출력하고, 출력된 복제체가 해당 플레이어에게 귀속된다.
- 타 플레이어의 복제체를 직접 조작할 수 없고, 자신의 복제체는 정상 조작된다.
- 일회성 작업(굴착/건설)은 서로의 복제체가 수행할 수 있다.
- 건물 운용 작업은 소유자의 복제체만 수행한다.
- 연구가 플레이어별로 분리되고, 언락하지 않은 건물은 주문할 수 없다.
- 굴착 자원은 서로 공유된다.
- 세이브·재접속·하드 싱크 후에도 소유권과 권한이 유지된다.
- 싱글플레이가 기존처럼 작동한다.
- 2인 기준 50주기 플레이가 가능하고, 세이브 손상이나 반복 크래시가 없다.

---

## 10. 초판에서 그대로 유지하는 항목

다음은 `plan(8).md`의 내용을 그대로 따른다.

- 6장 Claude Code 활용 방식 및 작업 단위 원칙
- 8.3 테스트 기록 형식
- 9.1 구조화 로그 필드 (Timestamp, GameTick, SessionId, PlayerId, CommandId, CommandType,
  NetworkObjectId, OwnerPlayerId, PermissionResult, ExecutionResult, WorldHash)
- 10.1 브랜치 전략 / 10.2 커밋 원칙 / 10.3 금지 작업
- 13장 후속 버전 로드맵

---

## 11. 다음 액션

완료:

| 담당 | 작업 | 상태 |
|---|---|---|
| henry | GitHub 포크 → `mkt-henry/oni-multi-mod` | ✅ |
| henry | .NET SDK 10.0.302 설치 | ✅ |
| henry | 게임에서 모드 활성화 및 로드 확인 | ✅ |
| Claude | 포크 clone + upstream remote 연결 | ✅ |
| Claude | Phase 0 빌드 검증 + 빌드 문서화 | ✅ |

남은 것:

| 담당 | 작업 | 필요 시점 |
|---|---|---|
| henry | Steam에서 ONI 자동 업데이트 비활성화 | 지금 (게임 갱신 시 빌드 파손) |
| henry | 테스트용 2번째 인스턴스 (2번째 Steam 계정 + PC) | Phase 3~4 |
| henry | ILSpy 설치 | Phase 7 (연구 분리) 전 |
| Claude | **Phase 1 발신자 컨텍스트 구현** | 진행 예정 |

### 저장소 운영

- `origin` = `mkt-henry/oni-multi-mod` (포크)
- `upstream` = `Lyraedan/Oxygen_Not_Included_Together`
- **`main`은 upstream 순수 미러로 유지한다.** 우리 작업은 전부 별도 브랜치에 쌓는다.
  이렇게 하면 `git merge upstream/main`이 항상 충돌 없이 깔끔하다.

---

## 부록 A. 검증 수준 명시

- **확인됨** — ONI 어셈블리에 존재하는 타입: `Telepad`, `Immigration`, `ImmigrantScreen`,
  `GlobalChoreProvider`, `ChoreConsumer`, `MinionIdentity`, `MinionResume`, `Research`,
  `ScheduleManager`, `WorldInventory`, `ClusterManager`, `WorldContainer`, `Clustercraft`,
  `TemplateLoader`, `TemplateContainer`, `WorldGen`, `ProcGen`,
  `Assignable`, `AssignmentGroup`, `AssignmentGroupController`, `IAssignableIdentity`,
  `MinionAssignablesProxy`
- **확인됨** — 베이스 프로젝트의 파일·라인 인용은 실제 소스 확인분
- **미확인** — 위 게임 타입들의 내부 구조(싱글톤 여부, 메서드 시그니처, 호출 경로).
  ILSpy 설치 후 Phase 1에서 확정한다. 확정 전까지 이 문서의 구현 세부는 잠정이다.

> ONI에 이미 `AssignmentGroup` 기반 배정 개념(침대·화장실 등)이 존재한다.
> 2.1의 건물 운용 규칙을 여기에 얹을 수 있는지 Phase 6에서 검토한다.
