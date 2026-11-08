
<img width="1150" height="300" alt="sfdsf" src="https://github.com/user-attachments/assets/dbf62ec8-0a90-4188-b2fc-2c016528f6d9" />

## Что в лаунчере?

- Возможность выбрать любую версию, начиная от 1.9, заканчивая последним релизом.
- Список модов Geode из папки `geode\mods`. По умолчанию открывается папка игры, которую запускает лаунчер; через список можно переключиться на папку версии прежнего лаунчера в `%LOCALAPPDATA%\GDLauncherSteam\versions`.
- Можно войти в свой steam. Вход нужен лишь для проверки наличия купленной Geometry dash в Steam.
- Доступен выбор языка: Русский / английский.
- Локальные темы из `Themes/<имя>/theme.json`, импорт ZIP в «Настройках», применение и удаление в «Мои темы».
- Можно создать свою тему по готовому шаблону.
- (Скоро можно будет выкладывать свои темы прямо на официальный Discord сервер. С лаучера же скачивать кастомные темы от других [некий Workshop так сказать])

## Сборка

Откройте `OpenGDLauncher.sln` в Visual Studio 2022 с компонентом **.NET desktop development** и .NET 8 SDK. Выберите `Release` и соберите решение. Альтернатива: `dotnet publish Launcher.UI/Launcher.UI.csproj -c Release -r win-x64 --self-contained false`.

## Кастомизация

Папка `ThemeKit/Blank` содержит PNG заготовки. `ThemeKit/GraphicDemo.zip` — устанавливаемая тестовая тема. Начните с [инструкции по созданию скина](КАК_СОЗДАТЬ_СКИН.md); полный список размеров — в [ThemeKit/README.md](ThemeKit/README.md). Отчёт о проверке — `TESTING.md`.

## Скриншоты 

<img width="500" height="335" alt="01" src="https://github.com/user-attachments/assets/da9f007a-ad13-414a-b737-5c15c6000308" />
<img width="500" height="335" alt="02" src="https://github.com/user-attachments/assets/f0e3c091-7def-4db3-b811-1ca6af5fc9ca" />
<img width="500" height="335" alt="03" src="https://github.com/user-attachments/assets/93765e6f-cd13-49a1-aa6c-1693cbe41728" />

## Отсебятина

Скоро будет(нет) сделан Workshop для кастомных тем от других пользователей. Так же официальный Discord сервер для обратной связи и публикации своих кастомных тем. 
