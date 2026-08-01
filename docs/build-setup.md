# 빌드 환경 구축 (검증 완료)

이 문서의 모든 내용은 이 PC에서 **실제로 빌드를 성공시킨 절차**다.
검증 일시: 2026-08-01 / 대상: `Lyraedan/Oxygen_Not_Included_Together` @ v0.7.3 계열 main

결과: `ONI_Together.dll` 4,065 KB 생성 (ILRepack 병합본), 오류 0 / 경고 14

---

## 1. 요구 사항

| 항목 | 상태 | 비고 |
|---|---|---|
| .NET SDK | **10.0.302** ✅ | `global.json`이 없어 SDK 10으로 빌드된다. SDK 8도 가능 |
| .NET 런타임 8.x | ✅ 8.0.24 | 전용 서버 프로젝트(`net8.0`)용 |
| .NET 런타임 6.x | ❌ 불필요 | 아래 3.1 우회책 적용 시 |
| git | ✅ 2.55.0 | MinVer가 요구. **PATH 등록 필요** (3.2) |
| Visual Studio | **불필요** | 전 프로젝트가 SDK 스타일. `dotnet build`로 충분 |
| ONI 설치 | ✅ | `D:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded` |

---

## 2. 최초 1회 설정

### 2.1 `Directory.Build.props.user` 생성

저장소 루트에 만든다. **`.gitignore` 대상이며 커밋하지 않는다.**
기본값(`Directory.Build.props.default`)이 `E:\SteamLibrary\...`를 가리키므로 반드시 필요하다.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
	<PropertyGroup>
		<GameLibsFolder>D:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed</GameLibsFolder>
		<ModFolder>$(UserProfile)\Documents\Klei\OxygenNotIncluded\mods\dev</ModFolder>
	</PropertyGroup>
</Project>
```

> `ModFolder`는 빌드 시 모드가 **자동 배포**되는 위치다.
> 게임에 영향을 주지 않고 빌드만 확인하려면 임시 폴더로 바꾼다.

### 2.2 로컬 dotnet 도구 복원

```powershell
dotnet tool restore
```

`jetbrains.refasmer.clitool` 2.0.3, `bepinex.assemblypublicizer.cli` 0.5.0-beta.2 가 복원된다.

---

## 3. 함정 3가지 (전부 실제로 겪음)

### 3.1 퍼블리사이저가 .NET 6 런타임을 요구한다

```
App: ...\bepinex.assemblypublicizer.cli\0.5.0-beta.2\tools\net6.0\any\BepInEx.AssemblyPublicizer.Cli.dll
Framework: 'Microsoft.NETCore.App', version '6.0.0' (x64)
error MSB3073: "dotnet tool run assembly-publicizer ..." 명령이 종료되었습니다(코드: -2147450730)
```

`.config/dotnet-tools.json`에서 두 도구 모두 **`"rollForward": false`** 로 고정되어 있고,
퍼블리사이저는 `net6.0` 타겟이다. .NET 6은 이 PC에 없다(8.0.24 / 10.0.3 / 10.0.10만 존재).

**해결 — 환경 변수 하나로 끝난다. .NET 6 설치 불필요:**

```powershell
$env:DOTNET_ROLL_FORWARD = "Major"
```

`.config/dotnet-tools.json`을 직접 수정해도 되지만, **추적 파일이라 upstream 머지 충돌이 생기므로 권장하지 않는다.**
환경 변수 방식은 저장소를 전혀 건드리지 않는다.

**이 오류를 방치하면 연쇄로 `CS0246: 'GameHashes'/'ConfirmDialogScreen'/'FileNameDialog' 형식을 찾을 수 없습니다`가 뜬다.**
퍼블리사이즈된 어셈블리가 생성되지 않아 게임 타입을 못 찾는 것이므로, 그쪽을 고치려 하면 안 된다.

### 3.2 MinVer가 PATH에서 git을 찾지 못한다

```
MinVer : error MINVER1007: "git" is not present in PATH.
```

git은 `C:\Program Files\Git\cmd\git.exe`에 설치되어 있으나 PowerShell PATH에 없다.

**해결:**

```powershell
$env:PATH = "C:\Program Files\Git\cmd;$env:PATH"
```

영구 적용하려면 시스템 환경 변수 PATH에 `C:\Program Files\Git\cmd`를 추가한다.

### 3.3 클린 상태에서 첫 빌드는 반드시 실패한다 (upstream 버그)

빌드 순서 문제다. `Shared.csproj`가 참조를 해석하는 시점에
`PublicisedAssembly\Assembly-CSharp_public.dll`이 아직 만들어지지 않았다.
퍼블리사이즈 타겟은 같은 빌드 안에서 나중에 실행된다.

**클린 상태에서 재현 확인:**

```
1차 빌드 → EXITCODE=1   (CS0246 다발)
2차 빌드 → EXITCODE=0   (성공)
```

**해결: 첫 설정 시 빌드를 두 번 돌린다.** 이후 증분 빌드는 1회로 정상 동작한다.
`PublicisedAssembly` 폴더를 지우면 다시 2회가 필요하다.

> 나중에 upstream에 리포트할 항목. 우리 포크에서 타겟 의존 관계를 고쳐도 되지만
> 머지 충돌 위험이 있으므로 MVP 이후로 미룬다.

---

## 4. 표준 빌드 명령

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$env:DOTNET_ROLL_FORWARD = "Major"
$env:PATH = "C:\Program Files\Git\cmd;$env:PATH"

dotnet tool restore
dotnet build ONI_Together.sln -c Release
# 클린 상태였다면 한 번 더
dotnet build ONI_Together.sln -c Release
```

**성공 시 출력** — `$(ModFolder)\ONI_Together_dev\` 아래:

```
ONI_Together.dll            4,065 KB   ← ILRepack 병합본
ONI_Together.pdb              359 KB
mod.yaml / mod_info.yaml               ← 빌드 시 자동 생성
assets\{windows,linux,mac}\oni_mp_ui_assets
translations\*.po
```

---

## 5. 주의

- 게임이 업데이트되면 `PublicisedAssembly`를 지우고 두 번 빌드해야 한다.
  Steam에서 ONI 자동 업데이트를 꺼 둘 것.
- `Directory.Build.props.user`는 개인 환경 파일이다. 커밋하지 않는다.
- 빌드는 `ModFolder`에 **자동 배포**된다. 게임 실행 중 빌드하면 DLL 잠금 오류가 날 수 있다.
