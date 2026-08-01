@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1

rem ---------------------------------------------------------------------------
rem  ONI 멀티플레이 모드 - 테스트 PC 설치 스크립트
rem
rem  이 파일과 같은 폴더에 ONI_Together_dev 폴더가 있어야 한다.
rem  두 PC의 모드 빌드가 다르면 프로토콜 검사에 걸리므로,
rem  마지막에 DLL 해시를 출력해 양쪽이 같은 빌드인지 대조할 수 있게 한다.
rem ---------------------------------------------------------------------------

echo.
echo ===============================================================
echo   ONI Together (분할 소유형 멀티플레이) - 테스트 PC 설치
echo ===============================================================
echo.

set "SRC=%~dp0ONI_Together_dev"

if not exist "%SRC%\ONI_Together.dll" (
    echo [실패] 같은 폴더에서 ONI_Together_dev 를 찾지 못했습니다.
    echo.
    echo        이 배치 파일과 ONI_Together_dev 폴더를 함께 복사해야 합니다.
    echo        현재 위치: %~dp0
    echo.
    goto :fail
)

rem --- 게임이 실행 중이면 DLL 이 잠겨 복사가 실패한다 ---
tasklist /fi "imagename eq OxygenNotIncluded.exe" 2>nul | find /i "OxygenNotIncluded.exe" >nul
if not errorlevel 1 (
    echo [실패] Oxygen Not Included 가 실행 중입니다.
    echo        게임을 완전히 종료한 뒤 다시 실행해 주세요.
    echo.
    goto :fail
)

rem --- 실제 내 문서 경로를 레지스트리에서 읽는다 (OneDrive 리디렉션 대응) ---
set "DOCS="
for /f "tokens=2,*" %%A in ('reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders" /v Personal 2^>nul') do set "DOCS=%%B"
call set "DOCS=%DOCS%"

if not defined DOCS set "DOCS=%USERPROFILE%\Documents"
if not exist "%DOCS%" set "DOCS=%USERPROFILE%\Documents"
if not exist "%DOCS%" set "DOCS=%USERPROFILE%\OneDrive\Documents"

set "KLEI=%DOCS%\Klei\OxygenNotIncluded"

if not exist "%KLEI%" (
    echo [실패] ONI 사용자 폴더를 찾지 못했습니다:
    echo        %KLEI%
    echo.
    echo        이 PC 에서 Oxygen Not Included 를 한 번 실행해
    echo        메인 메뉴까지 진입한 뒤 다시 시도해 주세요.
    echo.
    goto :fail
)

set "DEST=%KLEI%\mods\dev\ONI_Together_dev"

echo   원본 : %SRC%
echo   대상 : %DEST%
echo.

if not exist "%KLEI%\mods\dev" mkdir "%KLEI%\mods\dev" >nul 2>&1

rem --- /MIR 로 이전 버전 잔재까지 제거한다 (구버전 파일이 남으면 원인 불명 오류가 난다) ---
robocopy "%SRC%" "%DEST%" /MIR /NFL /NDL /NJH /NJS /NP >nul
rem robocopy 종료코드 8 이상만 실패
if errorlevel 8 (
    echo [실패] 복사 중 오류가 발생했습니다. robocopy 코드: %errorlevel%
    echo.
    goto :fail
)

if not exist "%DEST%\ONI_Together.dll" (
    echo [실패] 복사는 끝났지만 대상에 DLL 이 없습니다.
    echo.
    goto :fail
)

echo   [완료] 모드를 설치했습니다.
echo.

rem --- 빌드 동일성 확인용 해시 ---
echo ---------------------------------------------------------------
echo   빌드 확인용 SHA256 (양쪽 PC 에서 같아야 합니다)
echo ---------------------------------------------------------------
for /f "skip=1 tokens=*" %%H in ('certutil -hashfile "%DEST%\ONI_Together.dll" SHA256 2^>nul') do (
    if not defined HASH set "HASH=%%H"
)
echo   %HASH%
echo.

echo ---------------------------------------------------------------
echo   다음 단계
echo ---------------------------------------------------------------
echo   1. Oxygen Not Included 실행
echo   2. 메인 메뉴 - MODS 에서
echo      "Oxygen Not Included Together" 활성화 후 재시작
echo   3. 다른 모드는 모두 꺼 두세요 (변수 제거)
echo   4. 위 SHA256 값이 호스트 PC 와 같은지 대조
echo.
goto :done

:fail
echo ===============================================================
echo   설치하지 못했습니다.
echo ===============================================================
echo.
pause
exit /b 1

:done
echo ===============================================================
echo   설치 완료
echo ===============================================================
echo.
pause
exit /b 0
