Патч №15 — Окно «Чаты» + доступный список для JAWS

Что делает:
1) Добавляет окно: TelegramWin — Чаты (Views\ChatsWindow.xaml + .cs)
2) Добавляет ViewModel: ViewModels\ChatsViewModel.cs
   - Загружает первые 50 чатов (GetChats)
   - Подгружает название + последнее сообщение (GetChat)
   - Обновляет последнее сообщение по UpdateChatLastMessage
3) Окно авторизации (LoginWindow) автоматически закрывается и открывает «Чаты»,
   когда TDLib переходит в состояние Ready (Authorized).

Установка:
- Распакуйте ZIP прямо в E:\TelegramWin и согласитесь на замену файлов.

Сборка/запуск:
cd /d E:\TelegramWin
dotnet build -r win-x64
E:\TelegramWin\bin\Debug\net8.0-windows\win-x64\TelegramWin.exe

Как пользоваться (JAWS):
- В окне «Чаты» фокус сразу на списке.
- Стрелки вверх/вниз — чтение элементов.
- Каждый элемент читается как: «Название чата. Последнее сообщение».
