Патч №2 TelegramWin (TDLib_Fixed2) — устраняет ошибки сборки после Patch1

Что исправляет:
1) Убирает из проекта "старые" обращения к TdLibService: StartAsync, IsStarted, AuthorizationChanged и т.п.
2) Обновляет TdLibService так, чтобы он компилировался даже если в вашей TdApi тип CheckDatabaseEncryptionKey называется иначе.
3) Делает запуск приложения сразу в окне входа (LoginWindow), чтобы всё было единообразно и доступно для JAWS.

Куда копировать:
- Распакуйте архив в E:\TelegramWin
- Разрешите замену файлов.

Что делать в CMD:
1) cd /d E:\TelegramWin
2) rmdir /s /q bin
3) rmdir /s /q obj
4) dotnet restore
5) dotnet build
6) dotnet run

Если сборка прошла:
- сохраните в git:
  git status
  git add .
  git commit -m "Patch2: unify startup + TDLib service compatibility"

ВАЖНО: API_ID и API_HASH
Можно вставить позже. Сейчас главная цель — чтобы сборка проходила и окно входа работало.
