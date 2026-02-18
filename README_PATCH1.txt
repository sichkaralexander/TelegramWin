Патч TelegramWin (TDLib_Fixed2) — исправление сборки и авторизации

Что делает патч:
1) Исправляет работу с TDLib под пакет TDLib (TdClient.UpdateReceived + ExecuteAsync).
2) Убирает несуществующие в вашей сборке методы SendAsync/ReceiveAsync и параметр EnableStorageOptimizer.
3) Добавляет Live-region для JAWS и озвучку статусов.
4) Делает 3 шага авторизации: телефон → код → пароль 2FA (если включён).

Куда копировать:
- Распакуйте архив в E:\TelegramWin
- Разрешите замену файлов, если Windows спросит.

ВАЖНО: API_ID и API_HASH
1) Откройте файл appsettings.json (в корне проекта после распаковки).
2) Заполните:
   - ApiId: ваш числовой ID
   - ApiHash: ваша строка hash
3) Сохраните.

Как проверить:
1) Откройте CMD
2) Выполните:
   cd /d E:\TelegramWin
   dotnet run

Что должно быть:
- Проект собирается
- В окне входа JAWS озвучивает статус
- Появляются поля ввода в зависимости от шага (номер/код/пароль)

Git:
После успешного запуска сохраните изменения:
- git status
- git add .
- git commit -m "Fix TDLib auth flow + JAWS live status (patch1)"
