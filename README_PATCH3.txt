Патч №3 (TDLib_Fixed2) — исправляет ошибку CheckDatabaseEncryptionKey

Что исправляет:
- Убирает прямую ссылку на TdApi.CheckDatabaseEncryptionKey из Services\TdLibService.cs
- Использует reflection, поэтому сборка проходит даже если тип отсутствует в вашей TdApi.

Как установить:
1) Распакуйте архив в E:\TelegramWin
2) Разрешите замену файлов

Команды в CMD:
cd /d E:\TelegramWin
rmdir /s /q bin
rmdir /s /q obj
dotnet restore
dotnet build
dotnet run
