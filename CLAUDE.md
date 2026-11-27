# 부기 데스크펫 — 작업 규칙

## 가이드북 우선 (필수)
부기의 디자인은 부산시 소통캐릭터 부기 베이직 가이드북(`ref/bugi-guidebook.pdf`)이 정한다. 요약과 쪽수는 `docs/GUIDE_RULES.md`.

1. 캐릭터·색·선·표정·동작·로고 등 **디자인 요소를 만들거나 바꾸기 전에** `docs/GUIDE_RULES.md` 를 읽고, 해당 쪽을 PDF에서 직접 확인한다(이미지 PDF라 텍스트 추출이 안 된다. PyMuPDF 등으로 쪽을 이미지로 그려서 본다).
2. 사용자의 요청이 가이드북에 어긋나면 **먼저 경고**한다(어느 규정·몇 쪽인지, 무엇이 달라지는지). 사용자가 그래도 원하면 진행하되 어긋남을 코드 주석과 답변에 남긴다.
3. 색은 `BugiGuide`(`src/Guide.cs`)의 전용색상을 쓴다. `Rig*.cs` 에 숫자 색을 새로 쓰면 줄 끝에 출처(`//g:쪽수`, `//fx`, `//ref`)를 적는다.
4. 변경 후에는 `build.ps1` 이 돌려 주는 가이드 점검(경고는 노란색)과 `bugi.exe --guide-test`, `--selftest`, `--render-test` 결과 그림을 확인하고, 경고가 남으면 사용자에게 알린다.
5. 가이드북 저작권 조건: 출처표시 · 비상업적 이용 · 변형 등 2차적 저작물 작성 금지.

## 화면 언어 (한국어 · English)
- 사용자에게 보이는 글은 `Loc.T("한국어 원문")` / `Loc.TF("… {0}", 값)` 으로 감싸고 영어를 `src/Loc.En.cs` 에 더한다(한국어 원문이 키).
- 글을 이어 붙이지 않는다(어순이 다르다). 자리표시자(`{0}` 등)는 한국어·영어가 같아야 한다.
- 정적 필드에는 글을 넣지 않는다(언어 전환 시 바뀌도록 속성·메서드로 작성).
- 파일·폴더 이름(`부기_*.ini`, `%AppData%\부기`)과 모델이 읽는 키워드는 감싸지 않는다.
- 처음 실행하면 Windows 표시 언어를 따르며(`부기_설정.ini` 의 `Language=ko|en`), 영어 이름은 가이드북 p22 대로 **Boogi**, 부캉이는 **Bukangyee** 다.
- 글을 바꾸거나 더한 뒤에는 `bugi.exe --i18n-test C:\bt_i18n` 을 돌려 `ALL OK` 와 `i18n_*.png`(영어에서 글이 잘리지 않는지)를 확인한다.

## 문서 정책 (`README.md`)
- `README.md` 는 **일반 사용자용 안내**만 담는다(간단한 설치·실행법, 핵심 사용법, 최신 스크린캡처). 빌드·점검 모드·소스 구조·로컬 API 스펙 등 개발자용 안내는 적지 않는다.
- 초과근무 알림은 스크린샷을 싣지 않고 텍스트로만 간단히 설명한다.

## 빌드
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```
- .NET SDK 의 Roslyn(`csc.dll`)으로 .NET Framework 4.8 대상 단일 EXE(`output\bugi.exe`)를 만든다. 아이콘(`assets\bugi.ico`)이 없으면 자동으로 생성한다.
- 실행 중인 `output\bugi.exe` 가 잠겨 있을 때는 `-Out <다른경로>` 로 임시 빌드해 점검할 수 있다.

## 점검 모드 (개발용)
`--selftest` 등의 출력 폴더는 경로가 짧아야 한다(예: `C:\bt`). 아무 점검 모드에나 `--lang en` 을 붙여 영어 모드로 돌릴 수 있다.

```
bugi.exe --selftest <폴더>             설정·기록 저장, 친밀도 계산, 상태 전이, 메모·초과근무·AI 규칙 점검 (selftest_result.txt)
bugi.exe --ui-test <폴더>              모든 창 띄우기·실제 마우스 입력 점검, 화면 캡처 (마우스를 움직이니 주의)
bugi.exe --i18n-test <폴더>            English 화면 점검: 한글 남은 곳 · 사전 빠짐 · 영어 AI 프롬프트 · 언어 전환 (i18n_result.txt, i18n_*.png)
bugi.exe --render-test <폴더> [배율]   부기·부캉이 모든 모습 시트(sheet_*.png)와 연속 컷(motion_*.png) 저장
bugi.exe --guide-test [폴더]           가이드북 점검(전용색상·선 굵기·눈/부리/안경 비율) → guide_check.txt (경고 시 종료 코드 2)
bugi.exe --spec-test <폴더>            공식 규격 시트 비교용 회전 도면·얼굴 확대 저장 (spec_views.png, spec_face.png)
bugi.exe --note-test <폴더>            실제 메모 창(쓰기·서식·저장·알림·라이브러리)과 알림 부기 점검 (notetest_result.txt)
bugi.exe --alert-test <폴더> [문구]    알림 부기를 실제 화면에 띄워 캡처 (alert_screen.png, alert_render.png)
bugi.exe --note-shots <폴더>           README 스크린캡처 일괄 생성 (alert · notice · chat · note · library · menu · settings*.png)
bugi.exe --chat-live <폴더>            실제 Ollama 로 말투 품질·대화 흐름·기억·선톡 점검 (Ollama 필요)
```

## 로컬 연동 API · CLI (`src/ApiServer.cs`)
- 트레이 실행 중 `http://127.0.0.1:19480`(`%AppData%\부기\부기_api.json` 에 포트·PID 기록, `부기_설정.ini` `[연동]` `ApiOn=1`, `ApiPort=19480`)으로 로컬 앱 및 브라우저 확장(`*-extension://`) 요청을 받는다(일반 웹 `Origin` 은 `403`).
- 엔드포인트: `GET /api/health`, `POST/GET/DELETE /api/alert`, `GET/POST/PATCH/DELETE /api/notes`, `POST /api/notes/{id}/remind`, `GET /api/pet/actions`, `POST /api/pet/action`, `POST /api/chat`.
- CLI 전달: `bugi.exe --alert | --note | --action | --chat` 으로 실행 중인 부기에게 전달 후 즉시 종료.

## 주요 파일 구성 (`src/`)
- **캐릭터·렌더링**: `Rig.cs`(부기 규격 부위별 그리기) · `RigBukangyee.cs`(부캉이) · `RigOutfits.cs`(근무복 6종·아이템 3종) · `RigProps.cs`(응용동작 소품) · `Guide.cs`(가이드북 전용색상·자동 점검) · `SpecTools.cs` · `LogoArt.cs`/`LogoData.cs` · `Art.cs`
- **행동·동작**: `Pet.cs` · `PetFeatures.cs` · `PetPoses.cs` · `PetRender.cs` · `PetSkits.cs`(가이드북 응용동작 14종) · `PetSignature.cs`(캐릭터별 시그니처 기술) · `PetSecrets.cs`(숨은 반응·부딪힘) · `PetWindow.cs`
- **알림·초과근무**: `AlertPet.cs`(2배 큰 부기 네온 알림) · `Notice.cs`(정각 알림 카드·화면 가장자리 빛) · `AlarmComposer.cs` · `Overtime.cs` · `OvertimeUI.cs`
- **바탕화면 메모**: `NoteModel.cs` · `NoteStore.cs` · `NoteContent.cs` · `NoteEditor.cs` · `NoteLayout.cs` · `NoteWindow.cs` · `NoteManager.cs` · `NoteForms.cs`(`NoteLibraryView` 공용 라이브러리 · 기록 · 알림) · `NoteReminders.cs` · `MarkdownEditing.cs` · `MarkdownInline.cs`
- **AI 대화**: `ChatOptions.cs` · `ChatClient.cs`(Ollama 스트리밍) · `ExaoneClient.cs`(EXAONE API · DPAPI 키 암호화) · `OllamaSetup.cs`(자동 설치·업데이트·디지털 서명·여유 공간 확인) · `ChatPersona.cs` · `ChatModes.cs` · `ChatController.cs` · `PetChat.cs` · `ChatInput.cs` · `ChatHotkey.cs` · `ChatDiaryForm.cs` · `ChatMemoryForm.cs` · `ChatSound.cs` · `ChatJson.cs`
- **앱·설정·다국어·점검**: `Program.cs` · `App.cs` · `Options.cs` · `FormsSettings.cs` · `SettingsArt.cs` · `SettingsBehavior.cs` · `ChoiceStrip.cs` · `MenuStyle.cs` · `Native.cs` · `ApiServer.cs` · `Loc.cs` · `Loc.En.cs` · `DevToolsNotes.cs` · `DevToolsI18n.cs`
