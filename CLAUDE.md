# 부기 데스크펫 — 작업 규칙

## 가이드북 우선 (필수)
부기의 디자인은 부산시 소통캐릭터 부기 베이직 가이드북(`ref/bugi-guidebook.pdf`)이 정한다. 요약과 쪽수는 `docs/GUIDE_RULES.md`.

1. 캐릭터·색·선·표정·동작·로고 등 **디자인 요소를 만들거나 바꾸기 전에** `docs/GUIDE_RULES.md` 를 읽고, 해당 쪽을 PDF에서 직접 확인한다(이미지 PDF라 텍스트 추출이 안 된다. PyMuPDF 등으로 쪽을 이미지로 그려서 본다).
2. 사용자의 요청이 가이드북에 어긋나면 **먼저 경고**한다(어느 규정·몇 쪽인지, 무엇이 달라지는지). 사용자가 그래도 원하면 진행하되 어긋남을 코드 주석과 답변에 남긴다.
3. 색은 `BugiGuide`(`src/Guide.cs`)의 전용색상을 쓴다. `Rig*.cs` 에 숫자 색을 새로 쓰면 줄 끝에 출처(`//g:쪽수`, `//fx`, `//ref`)를 적는다.
4. 변경 후에는 `build.ps1` 이 돌려 주는 가이드 점검(경고는 노란색)과 `bugi.exe --guide-test`, `--selftest`, `--render-test` 결과 그림을 확인하고, 경고가 남으면 사용자에게 알린다.
5. 가이드북 저작권 조건: 출처표시 · 비상업적 이용 · 변형 등 2차적 저작물 작성 금지.

## 빌드
`powershell -ExecutionPolicy Bypass -File build.ps1` (출력 `output\bugi.exe`). 점검 모드는 README 의 "점검 모드" 참고. `--selftest` 의 폴더는 경로가 짧아야 한다(예: `C:\bt`).
