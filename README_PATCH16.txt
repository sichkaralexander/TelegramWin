Патч №16 — Открытие «Чаты» даже если TdLibService спрятан в ViewModel

Проблема:
После успешной авторизации окно чатов не открывается.
Причина: LoginWindow не находил TdLibService в DataContext (имя свойства другое).

Что делает патч:
- LoginWindow теперь ищет TdLibService автоматически:
  1) среди ВСЕХ публичных свойств
  2) среди публичных полей
  3) "проваливается" на 1 уровень внутрь вложенных объектов (Vm.Service, Vm.Td и т.п.)
- Когда TdLibService.IsAuthorized=true или приходит событие Authorized — открывает окно «Чаты» и закрывает авторизацию.

Установка:
Распаковать ZIP в E:\TelegramWin с заменой файла:
- Views\LoginWindow.xaml.cs

Сборка/запуск:
cd /d E:\TelegramWin
dotnet build -r win-x64
E:\TelegramWin\bin\Debug\net8.0-windows\win-x64\TelegramWin.exe
