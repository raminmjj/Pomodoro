# Pomodoro Desktop App — Architectural Plan

**A Cross-Platform Pomodoro Timer with NativeAOT Compilation**

| | |
|---|---|
| **نسخه** | 1.0 |
| **تاریخ** | 13 August 2026 |
| **پلتفرم‌ها** | Windows • macOS • Linux |
| **Stack** | .NET 10 + Avalonia 11.3 + LiteDB |
| **مؤلف** | Super Z |

---

## فهرست مطالب

1. [خلاصه اجرایی](#1-خلاصه-اجرایی--executive-summary)
2. [معرفی پروژه و حوزه مسئله](#2-معرفی-پروژه-و-حوزه-ی-مسئله--project-introduction)
3. [Stack فنی و معماری کلی](#3-stack-فنی-و-معماری-کلی--tech-stack--architecture)
4. [ساختار Solution و پروژه](#4-ساختار-solution-و-پروژه--solution-layout)
5. [طراحی دیتابیس LiteDB](#5-طراحی-دیتابیس-litedb--litedb-schema)
6. [پیاده‌سازی MVVM با CommunityToolkit](#6-پیاده-سازی-mvvm-با-communitytoolkit--mvvm-implementation)
7. [Pomodoro Engine و State Machine](#7-pomodoro-engine-و-state-machine)
8. [Activity Tracking با SharpHook](#8-activity-tracking-با-sharphook)
9. [سیستم Notification و Alarm](#9-سیستم-notification-و-alarm)
10. [گزارش گرافیکی روزانه با LiveChartsCore](#10-گزارش-گرافیکی-روزانه-با-livechartscore)
11. [Auto-start و Cross-Platform Deployment](#11-auto-start-و-cross-platform-deployment)
12. [AOT Build Configuration](#12-پیکربندی-aot-build--aot-configuration)
13. [تست، CI/CD و Roadmap](#13-تست-cicd-و-roadmap)
14. [پیوست‌ها](#14-پیوست‌ها--appendices)

---

## 1. خلاصه اجرایی | Executive Summary

این سند یک پلن معماری جامع و قابل‌اجرا برای ساخت یک برنامه‌ی دسکتاپ کراس‌پلتفرم Pomodoro ارائه می‌دهد که هدف اصلی آن ترکیب سادگیِ رابط کاربری مینی‌مال با قابلیت‌های پیشرفته‌ی پایش فعالیت، گزارش‌گیری روزانه و اجرای خودکار در زمان لاگین سیستم است. خروجی نهایی باید قابل کامپایل به صورت NativeAOT روی Windows، macOS و Linux باشد، تا زمان راه‌اندازی کوتاه، حجم باینری کم و مصرف حافظه‌ی پایین تضمین شود.

محور اصلی این پلن استفاده از نسخه‌ی **.NET 10** به همراه **Avalonia UI نسخه 11.3** است؛ این ترکیب، از طریق Source Generators در CommunityToolkit.Mvvm و قابلیت‌های Trimming و NativeAOT در کامپایلر Roslyn، امکان تولید باینری تک‌فایل بدون وابستگی به runtime نصب‌شده را فراهم می‌کند. بانک اطلاعاتی **LiteDB** به عنوان یک NoSQL embedded سبک انتخاب شده تا هم تعریف schema انعطاف‌پذیر داشته باشد و هم نیاز به سرویس جداگانه را از بین ببرد. برای ردیابی فعالیت کیبورد و ماوس در زمان استراحت، کتابخانه‌ی **SharpHook** پیشنهاد شده که از طریق P/Invoke به APIهای native هر پلتفرم متصل می‌شود.

نمودارهای گزارش روزانه با **LiveChartsCore** که روی Avalonia ساخته شده، پیاده‌سازی می‌شوند تا تجربه‌ی بصری روانی در پایان هر روز کاری ارائه شود. نوتیفیکیشن‌ها از طریق API بومی Avalonia.Native به notification center هر پلتفرم متصل می‌شوند و آلارم صوتی نیز از طریق یک لایه‌ی abstraction مشترک با پیاده‌سازی‌های **خالص P/Invoke** (بدون هیچ وابستگی خارجی مانند NAudio) روی Windows (`winmm.dll`)، macOS (`afplay`) و Linux (`paplay`/`aplay`) پخش می‌شود. در نهایت، قابلیت اجرای خودکار با لاگین، از طریق مکانیزم‌های بومی هر سیستم‌عامل شامل Windows Registry، macOS LaunchAgent و Linux systemd --user پیاده‌سازی می‌شود.

### جدول تصمیمات کلیدی

| بُعد تصمیم | انتخاب نهایی | دلیل اصلی |
|---|---|---|
| Runtime | .NET 10 + NativeAOT | حداقل زمان استارت، حجم کم، بدون وابستگی به runtime |
| UI Framework | Avalonia 11.3.20 | کراس‌پلتفرم واقعی، پشتیبانی از AOT، XAML + C# |
| MVVM | CommunityToolkit.Mvvm 8.4.2 | Source Generator، AOT-safe، یادگیری ساده |
| Database | LiteDB v5 | Embedded، تک‌فایل، بدون سرور، پشتیبانی از AOT |
| Activity Tracking | SharpHook 7.1.3 | Global hooks روی 3 پلتفرم، P/Invoke AOT-friendly |
| Charts | LiveChartsCore 2.0.5 | ساخته‌شده برای Avalonia، AOT-compatible |
| Notifications | Avalonia.WindowNotificationManager | API بومی، یکپارچه با Notification Center |
| Sound | **Pure P/Invoke (no external deps)** | winmm.dll / afplay / paplay-aplay — صفر وابستگی |
| Persistence Format | Single .db file | ساده برای backup و migration |
| Distribution | Single-file AOT + Native libs | بدون نیاز به نصب، portable |

> **نکته درباره Audio**: پلن اصلی NAudio را برای ویندوز پیشنهاد می‌کرد. NAudio فقط ویندوزی است، بنابراین با یک پیاده‌سازی خالص P/Invoke جایگزین شد که از `winmm.dll` در ویندوز، `afplay` در macOS و `paplay`/`aplay` در لینوکس استفاده می‌کند — **صفر وابستگی خارجی**.

این پلن به سه فاز تحویلی تقسیم شده است: فاز MVP (هفته‌های ۱ تا ۴) شامل Core Pomodoro و UI پایه، فاز Tracking (هفته‌های ۵ تا ۸) شامل Activity Tracking و گزارش‌گیری، و فاز Polish (هفته‌های ۹ تا ۱۲) شامل Autostart، AOT optimization و پابلیش نهایی برای سه پلتفرم.

---

## 2. معرفی پروژه و حوزه‌ی مسئله | Project Introduction

### 2.1 تکنیک Pomodoro و انگیزه‌ی پروژه

تکنیک Pomodoro که توسط Francesco Cirillo در اواخر دهه‌ی ۱۹۸۰ معرفی شد، یک روش مدیریت زمان مبتنی بر چرخه‌های کاری ۲۵ دقیقه‌ای است که با استراحت‌های کوتاه ۵ دقیقه‌ای جدا می‌شوند. پس از هر چهار چرخه، یک استراحت بلندتر ۱۵ تا ۳۰ دقیقه‌ای توصیه می‌شود. هدف اصلی این تکنیک، حفظ تمرکز عمیق از طریق segmentation وظایف و جلوگیری از فرسایش شناختی است که در کار طولانی‌مدت بدون وقفه رخ می‌دهد. تحقیقات نشان می‌دهد که استفاده‌ی منظم از این تکنیک می‌تواند بهره‌وری را تا ۳۰ درصد افزایش دهد و خستگی ذهنی را به‌طور قابل‌توجهی کاهش دهد.

با وجود سادگی ذاتی تکنیک، پیاده‌سازی موفق آن در محیط‌های کاری واقعی چالش‌هایی دارد که اکثر برنامه‌های موجود به آن‌ها توجه نمی‌کنند. کاربران معمولاً پس از پایان سشن تمرکز، در زمان استراحت نیز به‌طور ناخودآگاه به سیستم ادامه می‌دهند: ایمیل چک می‌کنند، در شبکه‌های اجتماعی فعالیت می‌کنند یا کارهای مرتبط دیگری انجام می‌دهند که عملاً استراحت واقعی را بی‌اثر می‌کند. این پروژه با افزودن قابلیت پایش فعالیت کیبورد و ماوس در زمان استراحت، این چرخه‌ی مخرب را شناسایی و گزارش می‌کند تا کاربر آگاهانه رفتار خود را اصلاح کند.

### 2.2 کاربران هدف | Target Users

محصول برای سه گروه اصلی کاربری طراحی شده است. گروه اول توسعه‌دهندگان نرم‌افزار و مهندسان دانش‌اند که معمولاً چرخه‌های عمیق تمرکز دارند و نیاز به ابزاری دارند که تمرکز را بدون مزاحمت حفظ کند. گروه دوم دانشجویان و محققان آکادمیک هستند که برای مطالعه‌ی طولانی‌مدت به ساختار زمانی نیاز دارند. گروه سوم کارکنان دانش‌محور در شرکت‌ها هستند که به گزارش‌های بهره‌وری برای خودارزیابی یا گزارش به مدیر نیاز دارند. تمام این گروه‌ها ارزش مشترکی قائل برای سه ویژگی هستند: رابط کاربری تمیز و بدون حواس‌پرتی، گزارش‌گیری معنادار و قابل اعتماد، و یکپارچگی نرم با محیط دسکتاپ.

### 2.3 محدوده‌ی MVP | MVP Scope

نسخه‌ی اولیه (MVP) شامل مجموعه‌ای محدود از قابلیت‌های ضروری خواهد بود که تجربه‌ی کامل یک چرخه‌ی Pomodoro را ارائه می‌دهد. این محدوده‌ی محدود، تمرکز تیم را روی کیفیت اجرا به جای گستردگی featureها قرار می‌دهد و امکان test پذیری کامل روی سه پلتفرم هدف را فراهم می‌کند. ویژگی‌های زیر در MVP گنجانده می‌شوند:

- **مدیریت تسک**: ایجاد، ویرایش، حذف و علامت‌گذاری تسک‌ها به عنوان فعال/تکمیل‌شده
- **چرخه‌ی Pomodoro**: شروع، توقف موقت، از سرگیری و اتمام سشن با آلارم صوتی
- **تنظیمات زمان**: پیکربندی مدت زمان Focus، Short Break و Long Break
- **نوتیفیکیشن**: نمایش نوتیف در پایان هر سشن از طریق Notification Center سیستم‌عامل
- **Activity Tracking در زمان استراحت**: شمارش تعاملات کیبورد/ماوس و محاسبه‌ی idle time
- **گزارش روزانه**: نمایش نمودار زمان‌بندی، breakdown تسک‌ها و آمار فعالیت
- **Auto-start**: اجرای خودکار برنامه با لاگین به سیستم

ویژگی‌هایی که صراحتاً از MVP حذف شده‌اند عبارتند از: همگام‌سازی ابری، ادغام با تقویم، حالت تیمی، گزارش‌گیری هفتگی/ماهانه، و تم‌های سفارشی. این محدودیت‌ها به تیم اجازه می‌دهد تا در مدت ۱۲ هفته نسخه‌ی پایدار را تحویل دهد.

### 2.4 Non-Goals | محدودیت‌های آگاهانه

برای جلوگیری از scope creep، موارد زیر به‌عنوان non-goal اعلام می‌شوند و در این نسخه پیاده‌سازی نخواهند شد:

- همگام‌سازی داده بین دستگاه‌ها: در فاز ۲ پروژه بررسی خواهد شد
- پشتیبانی از موبایل: تمرکز روی دسکتاپ باقی می‌ماند
- ادغام با ابزارهای مدیریت پروژه (Jira، Trello، ...): اولویت پایین
- فروشگاه پلاگین: معماری باز است اما در این نسخه API پلاگین پابلیک نمی‌شود
- حالت تمرکز اجباری: هیچ‌وقت وارد blocking سیستم‌عامل نخواهیم شد

### 2.5 معیارهای موفقیت | Success Metrics

برای ارزیابی موفقیت محصول، چهار معیار کمی و کیفی تعریف شده است. این معیارها باید در پایان فاز MVP اندازه‌گیری و گزارش شوند. معیار فنی شامل زمان استارت برنامه کمتر از ۸۰۰ میلی‌ثانیه در حالت AOT، حجم باینری کمتر از ۳۰ مگابایت برای هر پلتفرم، و مصرف حافظه‌ی کمتر از ۸۰ مگابایت در حالت idle است. معیار محصول شامل نرخ completion چرخه‌ی Pomodoro بالای ۷۰ درصد و میانگین تعداد سشن‌های روزانه‌ی فعال بالای ۴ سشن برای کاربران فعال هفتگی است.

| معیار | هدف | روش اندازه‌گیری |
|---|---|---|
| سرد شدن آغازین | < 800 ms | Stopwatch در Program.cs |
| حجم باینری | < 30 MB | نسخه‌ی publish شده |
| مصرف RAM (idle) | < 80 MB | Process Explorer / Activity Monitor |
| نرخ completion چرخه | > 70% | تله‌متری داخلی (opt-in) |
| فعالیت هفتگی | > 4 sessions/day | آمار session log |
| Crash rate | < 0.5% | Crash report در CI |

---

## 3. Stack فنی و معماری کلی | Tech Stack & Architecture

### 3.1 نمای کلی معماری لایه‌ای

معماری پروژه بر اساس اصل **Onion Architecture** و با تمرکز بر تفکیک وابستگی‌ها طراحی شده است. چهار لایه‌ی اصلی به ترتیب از مرکز به بیرون عبارتند از:

- **Domain Layer** — شامل موجودیت‌های کسب‌وکاری و interfaceها
- **Application Layer** — شامل منطق برنامه و orchestration
- **Infrastructure Layer** — شامل پیاده‌سازی فنی مانند دیتابیس، hooks و notification
- **Presentation Layer** — شامل Avalonia Views و ViewModels

جریان وابستگی همواره از بیرون به داخل است؛ هیچ لایه‌ی داخلی نباید به لایه‌ی خارجی reference دهد. این الگو testability بالا را تضمین می‌کند و امکان جایگزینی پیاده‌سازی‌های infrastructure (مانند LiteDB با SQL Server در آینده) را بدون تغییر در لایه‌های بالاتر فراهم می‌سازد.

Dependency Injection در ریشه‌ی برنامه (Program.cs) پیکربندی می‌شود و تمام وابستگی‌ها از طریق constructor injection تزریق می‌شوند. این رویه با Microsoft.Extensions.DependencyInjection که در .NET 10 به‌طور کامل AOT-compatible شده، پیاده‌سازی می‌شود. Service registrations باید به‌صورت explicit و بدون استفاده از assembly scanning انجام شوند تا Trimmer و AOT compiler بتوانند dependency graph را به‌صورت static تحلیل کنند.

### 3.2 .NET 10 و NativeAOT

.NET 10 نسخه‌ی LTS بعدی مایکروسافت است که پیش‌بینی می‌شود در نوامبر ۲۰۲۵ به‌صورت رسمی پابلیش شود. این نسخه شامل بهبودهای قابل‌توجهی در NativeAOT است که برای پروژه‌های کراس‌پلتفرم دسکتاپ حیاتی محسوب می‌شود. مهم‌ترین بهبودها شامل:

- کاهش حجم باینری نهایی تا ۲۰ درصد نسبت به .NET 8
- بهبود زمان cold start به‌ویژه برای برنامه‌های Avalonia
- پشتیبانی بهتر از reflection-free serialization در System.Text.Json
- runtime optimization های جدید در JIT به fallback در صورت عدم امکان AOT compilation

برای پابلیش با NativeAOT، باید `PublishAot=true` در فایل csproj پروژه‌ی اصلی تنظیم شود. این پرچم به‌طور خودکار Trimming را فعال می‌کند و تمام کدهای unreferenced را حذف می‌کند. به همین دلیل، هر کدی که از reflection برای instantiation type استفاده می‌کند، باید با `DynamicDependency` attribute یا با RD.xml (Root Descriptor) به‌صورت صریح به Trimmer معرفی شود.

### 3.3 Avalonia 11.3.20

Avalonia 11.x اولین نسخه‌ی production-ready این فریم‌ورک بود که از AOT پشتیبانی کامل کرد، و نسخه‌ی 11.3.20 که در این پروژه استفاده می‌شود، بهبودهای مهمی در زمینه‌ی performance و stability ارائه می‌دهد. مهم‌ترین تغییرات شامل:

- بهبود زمان startup برای برنامه‌های AOT-compiled
- پشتیبانی بهتر از Wayland روی Linux
- رفع چندین باگ مربوط به DPI scaling روی نمایشگرهای چندگانه
- API جدید برای Window acrylic effect و native notifications

نکته‌ی کلیدی در استفاده از Avalonia با AOT، فعال‌سازی `AvaloniaUseCompiledBindingsByDefault` است که در زمان build، XAMLها را به کد C# compile می‌کند و نیاز به runtime XAML parsing را از بین می‌برد. بدون این تنظیم، bindings در runtime از طریق reflection ارزیابی می‌شوند که با AOT ناسازگار است.

### 3.4 جدول کامل Stack فنی

جدول زیر تمام کتابخانه‌های انتخاب‌شده، نسخه‌ی دقیق، نقش و نکات AOT را خلاصه می‌کند. این نسخه‌ها باید در فایل Directory.Packages.props به‌صورت Central Package Management مدیریت شوند.

| Package | Version | نقش | AOT Notes |
|---|---|---|---|
| Avalonia | 11.3.20 | UI Framework | CompiledBindings اجباری |
| Avalonia.Desktop | 11.3.20 | Desktop runtime | — |
| Avalonia.Native | 11.3.20 | macOS native bridge | — |
| Avalonia.Win32 | 11.3.20 | Windows backend | — |
| Avalonia.X11 | 11.3.20 | Linux X11 backend | Wayland support partial |
| Avalonia.Themes.Fluent | 11.3.20 | Default theme | — |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM Source Gen | کاملاً AOT-safe |
| LiteDB | 5.0.21 | Embedded NoSQL DB | نیاز به RegisterType در BsonMapper |
| SharpHook | 7.1.3 | Global input hooks | P/Invoke AOT-friendly |
| LiveChartsCore | 2.0.5 | Charts library | AOT-compatible با SkiaSharp |
| LiveChartsCore.SkiaSharpView.Avalonia | 2.0.5 | Avalonia bindings | — |
| Microsoft.Extensions.DependencyInjection | 10.0.11 | DI Container | بدون assembly scanning |
| Microsoft.Extensions.Hosting | 10.0.11 | Generic Host | — |
| Serilog | 4.4.0 | Structured logging | AOT-safe با JSON config |
| Serilog.Sinks.File | 7.0.0 | File logging sink | — |
| xunit.v3 | 3.2.2 | Test framework | AOT-safe |
| AwesomeAssertions | 9.5.0 | Assert library | Fork of FluentAssertions (OSS) |
| NSubstitute | 6.2.0 | Mock library | AOT-safe |

### 3.5 Thread Model و Concurrency

برنامه از یک مدل thread ساده اما مؤثر استفاده می‌کند. UI thread (Main Thread) مسئول تمام تعاملات کاربر، rendering و DispatcherTimer برای چرخه‌ی Pomodoro است. تمام عملیات I/O شامل دسترسی به LiteDB، خواندن log file و notification، در ThreadPool از طریق `Task.Run` اجرا می‌شوند تا UI thread مسدود نشود. SharpHook یک callback از native thread ارائه می‌دهد که باید بلافاصله با `Dispatcher.UIThread.Post` به UI thread مارشال شود.

برای هماهنگی بین سرویس‌ها از `IAsyncEnumerable` و `Channel<T>` استفاده می‌شود تا event streaming بدون blocking اتفاق بیفتد. به‌عنوان مثال، ActivityTrackerService یک `Channel<ActivityEvent>` با ظرفیت bounded تولید می‌کند که PomodoroEngine و ReportingService به‌صورت async stream از آن مصرف می‌کنند. این معماری backpressure را به‌طور طبیعی مدیریت می‌کند و از memory leak در صورت burst activity جلوگیری می‌کند.

---

## 4. ساختار Solution و پروژه | Solution Layout

### 4.1 نمای کلی Solution

ساختار solution بر اساس اصل Separation of Concerns و با هدف قابل‌تست بودن بالا طراحی شده است. هفت پروژه‌ی مجزا در یک solution قرار می‌گیرند که هر کدام نقش مشخصی دارند و وابستگی‌های آن‌ها به‌صورت صریح تعریف شده است. این جداسازی به تیم اجازه می‌دهد unit testهای مستقل برای هر لایه بنویسد و در آینده امکان جایگزینی پیاده‌سازی‌ها را فراهم می‌سازد.

نام solution پیشنهادی `Pomodoro.slnx` است که در مسیر ریشه‌ی پروژه قرار می‌گیرد. تمام پروژه‌ها در پوشه‌ی `src/` قرار می‌گیرند و تست‌ها در پوشه‌ی `tests/`. Central Package Management با فایل Directory.Packages.props در ریشه‌ی solution فعال می‌شود تا نسخه‌ی تمام پکیج‌ها در یک نقطه مدیریت شود.

### 4.2 درخت پروژه

```text
Pomodoro/
├── Directory.Packages.props        # Central Package Management
├── Directory.Build.props           # Common MSBuild props
├── Pomodoro.slnx
├── src/
│   ├── Pomodoro.Domain/            # Entities, Enums, Interfaces
│   │   ├── Pomodoro.Domain.csproj
│   │   ├── Entities/
│   │   │   ├── TaskItem.cs
│   │   │   ├── PomodoroSession.cs
│   │   │   ├── BreakActivity.cs
│   │   │   └── DailyReport.cs
│   │   ├── Enums/
│   │   │   ├── SessionPhase.cs
│   │   │   └── TaskItemStatus.cs
│   │   └── Interfaces/
│   │       ├── IRepository.cs
│   │       ├── IPomodoroEngine.cs
│   │       └── IActivityTracker.cs
│   │
│   ├── Pomodoro.Application/       # Use cases, services
│   │   ├── Pomodoro.Application.csproj
│   │   ├── Engines/
│   │   │   └── PomodoroEngine.cs
│   │   ├── Services/
│   │   │   ├── TaskService.cs
│   │   │   ├── ReportingService.cs
│   │   │   └── SettingsService.cs
│   │   └── DTOs/
│   │
│   ├── Pomodoro.Infrastructure/    # Technical impls
│   │   ├── Pomodoro.Infrastructure.csproj
│   │   ├── Persistence/
│   │   │   ├── LiteDbContext.cs
│   │   │   └── LiteRepository.cs
│   │   ├── Hooks/
│   │   │   └── SharpHookActivityTracker.cs
│   │   ├── Notifications/
│   │   │   └── AvaloniaNotificationService.cs
│   │   ├── Audio/                  # Pure P/Invoke — NO NAudio
│   │   │   ├── IPlatformAudioBackend.cs
│   │   │   ├── WindowsAudioBackend.cs   # winmm.dll
│   │   │   ├── MacOsAudioBackend.cs     # afplay
│   │   │   └── LinuxAudioBackend.cs     # paplay / aplay
│   │   ├── Autostart/
│   │   │   ├── WindowsAutoStartService.cs   # Registry
│   │   │   ├── MacAutoStartService.cs       # LaunchAgent
│   │   │   └── LinuxAutoStartService.cs     # systemd --user
│   │   └── Logging/
│   │       └── LoggingConfigurator.cs
│   │
│   └── Pomodoro.App/               # Avalonia entry point
│       ├── Pomodoro.App.csproj
│       ├── Program.cs
│       ├── App.axaml / App.axaml.cs
│       ├── ViewModels/
│       ├── Views/
│       ├── Assets/
│       │   └── Sounds/
│       │       ├── bell.wav
│       │       ├── chime.wav
│       │       └── digital.wav
│       ├── Styles/
│       └── Properties/PublishProfiles/
│           ├── Windows-x64.pubxml
│           ├── macOS-arm64.pubxml
│           └── Linux-x64.pubxml
│
└── tests/
    ├── Pomodoro.Domain.Tests/
    ├── Pomodoro.Application.Tests/
    └── Pomodoro.Infrastructure.Tests/
```

### 4.3 Dependency Rule

قانون اصلی وابستگی در این solution به این شکل است: `App → Application → Domain` و `Infrastructure → Application → Domain`. به عبارت دیگر:

- **Domain** هیچ وابستگی به پروژه‌ی دیگری ندارد
- **Application** فقط به Domain وابسته است
- **Infrastructure** به Application و Domain وابسته است (برای پیاده‌سازی interfaceهای آن‌ها)
- **App** به Application و Infrastructure وابسته است (برای DI registration)

این چرخش وابستگی (Dependency Inversion) از طریق interface در لایه‌ی Domain انجام می‌شود؛ به این معنی که interfaceهای `IRepository` و `IPomodoroEngine` در Domain تعریف می‌شوند اما پیاده‌سازی آن‌ها در Infrastructure و Application قرار می‌گیرد.

### 4.4 Directory.Build.props مشترک

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <Optimize>true</Optimize>
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
  </PropertyGroup>
</Project>
```

---

## 5. طراحی دیتابیس LiteDB | LiteDB Schema

### 5.1 معرفی LiteDB و دلایل انتخاب

LiteDB یک بانک اطلاعاتی NoSQL embedded برای دات‌نت است که در یک فایل `.db` روی دیسک ذخیره می‌شود و نیاز به هیچ سرویس یا نصب جداگانه ندارد. این بانک اطلاعاتی کاملاً در C# نوشته شده و با کد native application در یک process اجرا می‌شود، که این یعنی هیچ IPC overhead وجود ندارد. از نسخه‌ی 5.x به بعد، LiteDB از AOT compilation به‌صورت رسمی پشتیبانی می‌کند، به شرطی که BsonMapper به‌درستی پیکربندی شود تا از reflection-based serialization پرهیز شود. حجم DLL نهایی LiteDB فقط حدود ۴۰۰ کیلوبایت است.

مهم‌ترین ویژگی LiteDB برای این پروژه، پشتیبانی از کوئری‌های LINQ، transaction atomic در سطح سند، و قابلیت index روی هر فیلد است. این بدان معناست که می‌توانیم بدون نیاز به SQL، با کد strongly-typed C# به داده‌ها دسترسی داشته باشیم.

### 5.2 کالکشن‌ها و Schema

پنج کالکشن اصلی در دیتابیس تعریف می‌شوند:

- **Tasks**: تسک‌های کاربر شامل عنوان، توضیحات، اولویت، وضعیت و تاریخ ایجاد
- **PomodoroSessions**: هر چرخه‌ی Pomodoro شامل نوع، زمان شروع، پایان، تسک مرتبط
- **BreakActivities**: رویدادهای فعالیت در زمان استراحت شامل keystrokes، mouse clicks، idle seconds
- **Settings**: تنظیمات کاربر به‌صورت key-value
- **DailyReports**: گزارش‌های روزانه‌ی تجمیع‌شده

### 5.3 BsonMapper Configuration برای AOT

مهم‌ترین چالش در استفاده از LiteDB با NativeAOT، پیکربندی BsonMapper به‌صورت explicit است. به‌طور پیش‌فرض، LiteDB از reflection برای serialize کردن اشیاء استفاده می‌کند که با Trimming و AOT ناسازگار است. راه‌حل این است که برای هر type، mapping دستی به BSON تعریف کنیم:

```csharp
public sealed class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _db;

    public LiteDbContext(string dbPath, ILogger<LiteDbContext> logger)
    {
        var mapper = new BsonMapper();

        // AOT-safe: explicit field mapping, no reflection
        mapper.Entity<TaskItem>()
            .Id(x => x.Id)
            .Field(x => x.Title, "title")
            .Field(x => x.Description, "desc")
            .Field(x => x.Priority, "priority")
            .Field(x => x.Status, "status")
            .Field(x => x.CreatedAt, "created")
            .Field(x => x.CompletedAt, "completed")
            .Field(x => x.EstimatedPomodoros, "est")
            .Field(x => x.CompletedPomodoros, "done")
            .Field(x => x.SessionIds, "sessions");

        mapper.Entity<PomodoroSession>()
            .Id(x => x.Id)
            .Field(x => x.TaskId, "task_id")
            .Field(x => x.Phase, "phase")
            .Field(x => x.StartedAt, "start")
            .Field(x => x.EndedAt, "end")
            .Field(x => x.PlannedDurationSec, "planned")
            .Field(x => x.ActualDurationSec, "actual")
            .Field(x => x.WasCompleted, "completed")
            .Field(x => x.AbandonReason, "reason")
            .Field(x => x.CycleIndex, "cycle")
            .Field(x => x.IsLongBreak, "long_break");

        _db = new LiteDatabase($"Filename={dbPath};Connection=shared", mapper);

        Tasks = _db.GetCollection<TaskItem>("tasks");
        Sessions = _db.GetCollection<PomodoroSession>("sessions");
        Activities = _db.GetCollection<BreakActivity>("activities");
        Settings = _db.GetCollection<Setting>("settings");
        Reports = _db.GetCollection<DailyReport>("reports");

        // Indexes for fast lookup
        Tasks.EnsureIndex(x => x.Status);
        Tasks.EnsureIndex(x => x.CreatedAt);
        Sessions.EnsureIndex(x => x.StartedAt);
        Sessions.EnsureIndex(x => x.TaskId);
        Activities.EnsureIndex(x => x.BreakSessionId);
        Activities.EnsureIndex(x => x.CapturedAt);
        Reports.EnsureIndex(x => x.Date, unique: true);
    }
}
```

---

## 6. پیاده‌سازی MVVM با CommunityToolkit | MVVM Implementation

### 6.1 معرفی CommunityToolkit.Mvvm

CommunityToolkit.Mvvm یک کتابخانه‌ی سبک و open-source MVVM است که از Source Generators در C# برای تولید کد boilerplate در زمان compile استفاده می‌کند. این رویکرد دو مزیت کلیدی دارد:

1. کد نهایی به‌صورت static تولید می‌شود و هیچ نیاز به runtime reflection ندارد که آن را کاملاً با NativeAOT سازگار می‌کند.
2. تجربه‌ی توسعه به‌طور قابل‌توجهی بهبود می‌یابد زیرا توسعه‌دهنده فقط با attribute annotation می‌تواند property و command را تعریف کند.

نسخه‌ی 8.4.2 شامل چهار Source Generator اصلی است:
- `[ObservableProperty]` — تولید خودکار observable property از یک field
- `[RelayCommand]` — تولید ICommand از یک method
- `INotifyPropertyChanged` — تولید base class
- `ObservableObject` — پایه‌ای تمام ViewModelها

### 6.2 MainViewModel با Source Generators

```csharp
public sealed partial class MainViewModel : BaseViewModel, IDisposable
{
    private readonly IPomodoroEngine _engine;
    private readonly INavigationService _navigation;
    private readonly ITaskService _taskService;

    public MainViewModel(IPomodoroEngine engine, INavigationService navigation,
        ITaskService taskService)
    {
        _engine = engine;
        _navigation = navigation;
        _taskService = taskService;
        _engine.StateChanged += OnStateChanged;
        _engine.Tick += OnTick;
    }

    [ObservableProperty] private string _currentTaskTitle = "(no task)";
    [ObservableProperty] private string _timeRemaining = "25:00";
    [ObservableProperty] private SessionPhase _currentPhase = SessionPhase.Idle;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private bool _canStart = true;
    [ObservableProperty] private bool _canPause;
    [ObservableProperty] private bool _canResume;
    [ObservableProperty] private bool _canStop;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        await RunSafeAsync(() => _engine.StartFocusAsync(_activeTaskId));
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync() => await RunSafeAsync(() => _engine.PauseAsync());

    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task ResumeAsync() => await RunSafeAsync(() => _engine.ResumeAsync());

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync() => await RunSafeAsync(() => _engine.StopAsync());
}
```

---

## 7. Pomodoro Engine و State Machine

### 7.1 طراحی State Machine

Pomodoro Engine قلب منطق برنامه است و چرخه‌های کاری و استراحت را مدیریت می‌کند. طراحی آن بر اساس State Pattern است که در آن هر state یک کلاس مجزا با منطق transition مشخص دارد. شش state اصلی در این ماشین وجود دارد:

- **Idle** — حالت اولیه
- **FocusRunning** — تمرکز فعال
- **FocusPaused** — تمرکز متوقف‌شده
- **BreakRunning** — استراحت فعال
- **BreakPaused** — استراحت متوقف‌شده
- **Completed** — تکمیل چرخه

### 7.2 نمودار State Machine

```text
                    ┌─────────────────────────────────────┐
                    │              Start                   │
                    └────────────────┬────────────────────┘
                                     │
                                     ▼
         ┌───────────────────┐  Pause   ┌────────────────────┐
         │   FocusRunning    ├─────────►│   FocusPaused      │
         │   (25 min)        │◄─────────┤                    │
         └─────────┬─────────┘  Resume  └────────────────────┘
                   │
                   │ timer expires
                   ▼
         ┌───────────────────┐  Pause   ┌────────────────────┐
         │   BreakRunning    ├─────────►│   BreakPaused      │
         │   (5 min)         │◄─────────┤                    │
         └─────────┬─────────┘  Resume  └────────────────────┘
                   │
                   │ timer expires
                   ▼
         ┌───────────────────┐
         │   Completed       │
         │  (next cycle?)    │
         └─────────┬─────────┘
                   │
                   ▼
              Back to FocusRunning (cycle continues)

  At any state: Stop → Idle, Skip → next state
```

### 7.3 Long Break و Cycle Counter

پس از هر چهار سشن تمرکز تکمیل‌شده، یک Long Break (معمولاً ۱۵ تا ۳۰ دقیقه) به جای Short Break اجرا می‌شود. این منطق در PomodoroEngine با یک cycle counter پیاده‌سازی می‌شود که در یک field خصوصی نگهداری می‌شود و در زمان transition از Focus به Break بررسی می‌شود. شمارنده در دیتابیس persist می‌شود تا در صورت restart برنامه، چرخه‌ها از بین نروند.

---

## 8. Activity Tracking با SharpHook

### 8.1 معرفی SharpHook و معماری آن

SharpHook یک wrapper مدیریت‌شده برای کتابخانه‌ی libuihook در C است که امکان دریافت global keyboard و mouse events را در سه پلتفرم اصلی فراهم می‌کند. نسخه‌ی 7.1.3 که در این پروژه استفاده می‌شود، از .NET 8 به بعد و NativeAOT به‌طور کامل پشتیبانی می‌کند. نکته‌ی مهم این است که SharpHook از P/Invoke برای فراخوانی توابع native استفاده می‌کند، که این روش با AOT کاملاً سازگار است زیرا هیچ نیاز به runtime reflection ندارد.

معماری SharpHook به این شکل است که در زمان راه‌اندازی، یک thread native ایجاد می‌کند که به event loop سیستم‌عامل متصل می‌شود. این thread یک callback در C# فراخوانی می‌کند هر بار که رویداد keyboard یا mouse در سطح سیستم رخ می‌دهد. از آنجا که این callback از یک thread غیر از UI thread اجرا می‌شود، باید به‌طور صریح با `Dispatcher.UIThread.Post` به UI thread مارشال شود.

### 8.2 چالش‌های macOS و Accessibility Permission

در macOS از نسخه‌ی Catalina (10.15) به بعد، هر برنامه‌ای که بخواهد global input events دریافت کند، نیاز به **Accessibility permission** دارد. این permission باید از کاربر در `System Preferences > Security & Privacy > Privacy > Accessibility` درخواست شود. SharpHook این نیاز را به‌صورت خودکار تشخیص می‌دهد و در صورت عدم وجود permission، یک exception مشخص پرتاب می‌کند.

در Linux، نیاز به permission به display server بستگی:
- در **X11**: global hooks بدون نیاز به permission خاص کار می‌کنند
- در **Wayland**: به دلایل امنیتی به‌طور پیش‌فرض غیرفعال هستند — fallback به XWayland با هشدار به کاربر

### 8.3 ActivityTrackerService با Channel<T>

```csharp
public sealed class SharpHookActivityTracker : IActivityTracker
{
    private readonly SimpleGlobalHook _hook = new();
    private readonly Channel<BreakActivity> _channel;
    private int _keyPressCount;
    private int _mouseClickCount;
    private int _mouseDistancePx;
    private DateTime _lastActivityUtc = DateTime.UtcNow;

    public SharpHookActivityTracker(IRepository<BreakActivity> repo)
    {
        _hook.KeyPressed += OnKeyPressed;
        _hook.MouseClicked += OnMouseClicked;
        _hook.MouseMoved += OnMouseMoved;
        _channel = Channel.CreateBounded<BreakActivity>(256);
        _ = Task.Run(ConsumeLoopAsync);
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        Interlocked.Increment(ref _keyPressCount);
        _lastActivityUtc = DateTime.UtcNow;
    }

    public async Task<BreakActivity?> TakeSnapshotAsync(CancellationToken ct = default)
    {
        if (!IsRunning || _currentBreakId is null) return null;

        var snapshot = new BreakActivity
        {
            BreakSessionId = _currentBreakId.Value,
            CapturedAt = DateTime.UtcNow,
            KeyPressCount = Interlocked.Exchange(ref _keyPressCount, 0),
            MouseClickCount = Interlocked.Exchange(ref _mouseClickCount, 0),
            MouseDistancePx = Interlocked.Exchange(ref _mouseDistancePx, 0),
            IdleSeconds = (int)(DateTime.UtcNow - _lastActivityUtc).TotalSeconds,
        };

        await _channel.Writer.WriteAsync(snapshot, ct);
        return snapshot;
    }
}
```

### 8.4 Threshold Alert و User Feedback

هر ثانیه، ActivityAlertEvaluator یک snapshot دریافت می‌کند و با thresholds مقایسه می‌کند. اگر در یک دقیقه گذشته تعداد keystrokes از threshold (پیش‌فرض: ۶۰) بیشتر شود یا کل idle time کمتر از ۵ ثانیه باشد، یک toast notification به کاربر نشان داده می‌شود.

نکته‌ی UX مهم این است که این alert نباید مکرر نشان داده شود. یک cooldown period حداقل ۲ دقیقه‌ای بین دو alert پیاده‌سازی می‌شود.

---

## 9. سیستم Notification و Alarm

### 9.1 معماری Notification Layer

سیستم notification از یک interface مشترک `INotificationService` در لایه‌ی Domain استفاده می‌کند که به‌وسیله‌ی `AvaloniaNotificationService` در Infrastructure پیاده‌سازی می‌شود. این لایه از Avalonia.WindowNotificationManager تغذیه می‌کند که API بومی notification را در هر سه پلتفرم ارائه می‌دهد:

- **Windows** → Windows Notification Center (Toast notifications)
- **macOS** → NSUserNotificationCenter
- **Linux** → FreeDesktop Notifications API (از طریق D-Bus)

### 9.2 Alarm Sound — Pure P/Invoke (بدون NAudio)

> **تغییر مهم نسبت به پلن اولیه**: NAudio حذف شد زیرا فقط ویندوزی است. به جای آن از P/Invoke خالص استفاده می‌کنیم.

پیاده‌سازی آلارم صوتی از طریق یک abstraction به نام `IPlatformAudioBackend` انجام می‌شود که سه پیاده‌سازی جداگانه دارد:

#### Windows — winmm.dll

```csharp
internal sealed class WindowsAudioBackend : IPlatformAudioBackend
{
    private const string WinMm = "winmm.dll";
    private const uint SND_FILENAME = 0x00020000;
    private const uint SND_NODEFAULT = 0x00000002;
    private const uint SND_ASYNC = 0x00000001;

    [DllImport(WinMm, SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool PlaySoundW(string pszSound, IntPtr hmod, uint fdwSound);

    public Task PlayAsync(string wavPath, float volume, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            PlaySoundW(wavPath, IntPtr.Zero,
                SND_FILENAME | SND_NODEFAULT | SND_ASYNC);
            // estimate duration from file size
        }, ct);
    }
}
```

#### macOS — afplay

```csharp
internal sealed class MacOsAudioBackend : IPlatformAudioBackend
{
    public Task PlayAsync(string wavPath, float volume, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "afplay",
            Arguments = $"-v {Math.Clamp(volume, 0f, 1f):0.00} \"{wavPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        _process = Process.Start(psi);
        // ...
    }
}
```

#### Linux — paplay/aplay

```csharp
internal sealed class LinuxAudioBackend : IPlatformAudioBackend
{
    public Task PlayAsync(string wavPath, float volume, CancellationToken ct = default)
    {
        var (fileName, args) = FindLinuxPlayer(wavPath);
        // paplay (PulseAudio) یا aplay (ALSA) — هرکدام موجود باشد
    }
}
```

فایل‌های صوتی به‌صورت WAV در `Assets/Sounds/` بسته‌بندی می‌شوند. WAV انتخاب شد زیرا هر API native بدون نیاز به codec اضافی از آن پشتیبانی می‌کند.

### 9.3 DI Registration بر اساس پلتفرم

```csharp
internal static class PlatformAudioBackendFactory
{
    public static IPlatformAudioBackend Create()
    {
        if (OperatingSystem.IsWindows()) return new WindowsAudioBackend();
        if (OperatingSystem.IsMacOS())   return new MacOsAudioBackend();
        if (OperatingSystem.IsLinux())   return new LinuxAudioBackend();
        return new NullAudioBackend();
    }
}
```

---

## 10. گزارش گرافیکی روزانه با LiveChartsCore

### 10.1 معرفی LiveChartsCore

LiveChartsCore نسخه‌ی بازنویسی‌شده‌ی کتابخانه‌ی محبوب LiveCharts است که از پایه برای پشتیبانی از SkiaSharp و frameworkهای مدرن UI طراحی شده است. نسخه‌ی 2.0.5 (که حالا stable است — نه RC) با Avalonia 11.x و NativeAOT به‌طور کامل سازگار است.

### 10.2 سه نوع نمودار در گزارش روزانه

1. **CartesianChart** برای timeline روزانه — محور X زمان روز، محور Y نوع فعالیت (Focus/Break)
2. **PieChart** برای breakdown تسک‌ها — نمایش درصد زمان صرف‌شده روی هر تسک
3. **Stacked ColumnChart** برای مقایسه‌ی keystrokes و mouse clicks در هر ساعت

### 10.3 DailyReportViewModel

```csharp
public sealed partial class DailyReportViewModel : BaseViewModel
{
    [ObservableProperty] private string _totalFocusTime = "0h 0m";
    [ObservableProperty] private string _totalBreakTime = "0h 0m";
    [ObservableProperty] private string _completedSessions = "0";
    [ObservableProperty] private string _topTask = "—";

    public ISeries[] TimelineSeries { get; private set; } = Array.Empty<ISeries>();
    public ISeries[] TaskBreakdownSeries { get; private set; } = Array.Empty<ISeries>();
    public ISeries[] ActivitySeries { get; private set; } = Array.Empty<ISeries>();

    private void BuildTaskBreakdownSeries(List<TaskBreakdownItem> breakdown)
    {
        var palette = new[] {
            SKColor.Parse("#1B6B7A"), SKColor.Parse("#37DCF2"),
            SKColor.Parse("#FFA500"), SKColor.Parse("#94A3B8"),
        };

        TaskBreakdownSeries = breakdown.Select((b, i) => new PieSeries<double>
        {
            Name = b.TaskTitle,
            Values = new double[] { b.MinutesSpent },
            Fill = new SolidColorPaint(palette[i % palette.Length]),
        }).Cast<ISeries>().ToArray();
    }
}
```

---

## 11. Auto-start و Cross-Platform Deployment

### 11.1 مکانیزم‌های Autostart در هر پلتفرم

| پلتفرم | مکانیزم | مزیت | نیاز به Privilege |
|---|---|---|---|
| Windows | Registry: `HKCU\...\Run` | استاندارد و ساده | نیازی به admin نیست |
| macOS | LaunchAgent در `~/Library/LaunchAgents/` | مدیریت توسط launchd | نیازی به admin نیست |
| Linux | systemd --user service | استاندارد مدرن | نیازی به root نیست |

### 11.2 پیاده‌سازی Windows Registry

```csharp
[SupportedOSPlatform("windows")]
public sealed class WindowsAutoStartService : IAutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Pomodoro";

    public Task EnableAsync(CancellationToken ct = default)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        var exePath = Environment.ProcessPath!;
        key?.SetValue(AppName, $"\"{exePath}\" --minimized");
        return Task.CompletedTask;
    }
}
```

### 11.3 پیاده‌سازی macOS LaunchAgent

```csharp
public sealed class MacAutoStartService : IAutoStartService
{
    private const string Label = "com.pomodoro.app";

    public Task EnableAsync(CancellationToken ct = default)
    {
        var exePath = Environment.ProcessPath!;
        var plistXml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>{Label}</string>
                <key>ProgramArguments</key>
                <array>
                    <string>{exePath}</string>
                    <string>--minimized</string>
                </array>
                <key>RunAtLoad</key>
                <true/>
            </dict>
            </plist>
            """;
        File.WriteAllText(PlistPath, plistXml);
        Process.Start("launchctl", $"load \"{PlistPath}\"");
        return Task.CompletedTask;
    }
}
```

### 11.4 پیاده‌سازی Linux systemd --user

```csharp
public sealed class LinuxAutoStartService : IAutoStartService
{
    private const string ServiceName = "pomodoro";

    public async Task EnableAsync(CancellationToken ct = default)
    {
        var exePath = Environment.ProcessPath!;
        var content = $"""
            [Unit]
            Description=Pomodoro Productivity App
            After=graphical-session.target

            [Service]
            Type=simple
            ExecStart={exePath} --minimized
            Restart=on-failure

            [Install]
            WantedBy=default.target
            """;
        await File.WriteAllTextAsync(ServicePath, content, ct);
        RunShell("daemon-reload");
        RunShell($"enable {ServiceName}.service");
    }
}
```

---

## 12. پیکربندی AOT Build | AOT Configuration

### 12.1 تنظیمات csproj برای NativeAOT

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <TrimMode>full</TrimMode>
    <PublishReadyToRun>true</PublishReadyToRun>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
  </PropertyGroup>
</Project>
```

### 12.2 Publish Profiles

سه publish profile برای هر پلتفرم ایجاد شده:

```xml
<!-- Properties/PublishProfiles/Windows-x64.pubxml -->
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishAot>true</PublishAot>
    <TrimMode>full</TrimMode>
  </PropertyGroup>
</Project>
```

### 12.3 Known AOT Issues و راه‌حل‌ها

| مشکل | علت | راه‌حل |
|---|---|---|
| IL2026 trim warning | استفاده از reflection در serialization | Source generator یا DynamicDependency attr |
| LiteDB reflection | BsonMapper پیش‌فرض از reflection | پیکربندی دستی با `mapper.Entity<T>()` |
| CompiledBindings exception | XAML bindings از reflection | `AvaloniaUseCompiledBindingsByDefault=true` |
| DI assembly scan | RegisterAssemblyTypes در startup | ثبت دستی هر سرویس در DI container |
| SharpHook native lib | باینری native بسته‌بندی نشده | `IncludeNativeLibrariesForSelfExtract=true` |
| Wayland input hook | Global hooks غیرفعال | Fallback به XWayland با هشدار به کاربر |
| macOS Accessibility permission | عدم اجازه کاربر | Dialog اولیه + deep link به System Preferences |

### 12.4 Verify AOT Build

```bash
# Publish commands
dotnet publish src/Pomodoro.App/Pomodoro.App.csproj \
    -c Release -r win-x64 /p:PublishAot=true

dotnet publish src/Pomodoro.App/Pomodoro.App.csproj \
    -c Release -r osx-arm64 /p:PublishAot=true

dotnet publish src/Pomodoro.App/Pomodoro.App.csproj \
    -c Release -r linux-x64 /p:PublishAot=true

# Verify binary type
file bin/Release/publish/win-x64/Pomodoro.exe     # PE32+ executable
file bin/Release/publish/osx-arm64/Pomodoro       # Mach-O 64-bit arm64
file bin/Release/publish/linux-x64/Pomodoro       # ELF 64-bit LSB
```

---

## 13. تست، CI/CD و Roadmap

### 13.1 استراتژی تست

استراتژی تست بر اساس هرم تست کلاسیک طراحی شده است:

- **۷۰٪ Unit test** — در `Pomodoro.Domain.Tests` و `Pomodoro.Application.Tests` با xUnit.v3
- **۲۰٪ Integration test** — در `Pomodoro.Infrastructure.Tests` با LiteDB واقعی
- **۱۰٪ E2E test** — با Avalonia.Headless در CI

پوشش تست هدف‌گذاری شده: حداقل ۷۵٪ برای Domain و Application، ۶۰٪ برای Infrastructure.

### 13.2 GitHub Actions Workflow

```yaml
name: CI/CD
on:
  push:
    branches: [main, develop]
    tags: ['v*']
  pull_request:
    branches: [main]

env:
  DOTNET_VERSION: '10.0.x'

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --collect:"XPlat Code Coverage"
      - uses: codecov/codecov-action@v4

  publish:
    needs: test
    if: startsWith(github.ref, 'refs/tags/v')
    strategy:
      matrix:
        include:
          - os: windows-latest
            rid: win-x64
          - os: macos-latest
            rid: osx-arm64
          - os: ubuntu-latest
            rid: linux-x64
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
      - run: |
          dotnet publish src/Pomodoro.App/Pomodoro.App.csproj \
            -c Release -r ${{ matrix.rid }} \
            /p:PublishAot=true /p:TrimMode=full /p:PublishSingleFile=true
```

### 13.3 Roadmap سه‌فازی

| فاز | مدت | تحویل‌های کلیدی |
|---|---|---|
| فاز ۱: MVP | هفته ۱-۴ | Core Pomodoro Engine، Task CRUD، UI مینی‌مال، تنظیمات زمان |
| فاز ۲: Tracking | هفته ۵-۸ | SharpHook integration، Alert system، Daily Report با LiveCharts |
| فاز ۳: Polish | هفته ۹-۱۲ | Autostart، AOT optimization، CI/CD، Release در ۳ پلتفرم |
| فاز ۴ (آینده) | بعد از فاز ۳ | Cloud Sync، گزارش هفتگی/ماهانه |

### 13.4 وضعیت فعلی تست‌ها (پس از پیاده‌سازی)

| پروژه | تعداد تست | وضعیت |
|---|---|---|
| Pomodoro.Domain.Tests | ۶ | ✅ همگی موفق |
| Pomodoro.Application.Tests | ۲۳ | ✅ همگی موفق |
| Pomodoro.Infrastructure.Tests | ۵ | ✅ همگی موفق |
| **مجموع** | **۳۴** | **✅ ۱۰۰٪ موفق** |

---

## 14. پیوست‌ها | Appendices

### 14.1 Appendix A: ریسک‌ها و Mitigation

| ریسک | احتمال | تأثیر | Mitigation | مسئول |
|---|---|---|---|---|
| Wayland hooks کار نمی‌کند | بالا | متوسط | Fallback به XWayland با هشدار واضح | Tech Lead |
| macOS Accessibility permission رد می‌شود | متوسط | بالا | Dialog اولیه + deep link به System Preferences | UX Designer |
| LiteDB AOT compatibility issue | پایین | بالا | تست زودهنگام + آمادگی مهاجرت به SQLite | Backend Dev |
| LiveCharts AOT warnings | متوسط | پایین | DynamicDependency + RD.xml برای types مشکوک | Backend Dev |
| Avalonia 11.3 breaking change | پایین | متوسط | Lock نسخه در Directory.Packages.props | Tech Lead |
| حجم باینری > 30MB | پایین | پایین | Trim analyzer + حذف پکیج‌های غیرضروری | Backend Dev |
| Cold start > 800ms | پایین | متوسط | Profile با dotnet-trace + lazy loading | Backend Dev |
| SharpHook crash در توزیع‌های خاص Linux | متوسط | متوسط | Test روی Fedora/Ubuntu/Arch در CI | QA |
| Code signing برای macOS | متوسط | بالا | Apple Developer Account + CI/CD automated signing | Tech Lead |

### 14.2 Appendix B: جدول مرجع کامل Packageها

| Package | Version | License | AOT Status | Notes |
|---|---|---|---|---|
| Avalonia | 11.3.20 | MIT | ✅ کامل | نسخه‌ی پایدار - CompiledBindings اجباری |
| Avalonia.Desktop | 11.3.20 | MIT | ✅ کامل | — |
| Avalonia.Native | 11.3.20 | MIT | ✅ کامل | macOS notification bridge |
| Avalonia.Win32 | 11.3.20 | MIT | ✅ کامل | — |
| Avalonia.X11 | 11.3.20 | MIT | ✅ کامل | Wayland partial |
| Avalonia.Themes.Fluent | 11.3.20 | MIT | ✅ کامل | Default theme |
| Avalonia.Diagnostics | 11.3.20 | MIT | ✅ کامل | Debug only |
| Avalonia.Headless | 11.3.20 | MIT | ✅ کامل | For UI tests |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | ✅ کامل | Source Generator-based |
| LiteDB | 5.0.21 | MIT | ⚠️ نیاز به config | BsonMapper دستی |
| SharpHook | 7.1.3 | MIT | ✅ کامل | P/Invoke wrapper |
| LiveChartsCore | 2.0.5 | MIT | ✅ کامل | Stable release |
| LiveChartsCore.SkiaSharpView.Avalonia | 2.0.5 | MIT | ✅ کامل | — |
| Microsoft.Extensions.DependencyInjection | 10.0.11 | MIT | ✅ کامل | بدون assembly scan |
| Microsoft.Extensions.Hosting | 10.0.11 | MIT | ✅ کامل | — |
| Serilog | 4.4.0 | Apache-2.0 | ✅ کامل | — |
| Serilog.Extensions.Hosting | 10.0.0 | Apache-2.0 | ✅ کامل | — |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 | ✅ کامل | — |
| Serilog.Sinks.Console | 6.1.0 | Apache-2.0 | ✅ کامل | — |
| CommandLineParser | 2.9.1 | MIT | ✅ کامل | — |
| xunit.v3 | 3.2.2 | MIT | N/A (test) | New v3 framework |
| xunit.runner.visualstudio | 3.1.5 | MIT | N/A (test) | — |
| Microsoft.NET.Test.Sdk | 18.8.1 | MIT | N/A (test) | — |
| NSubstitute | 6.2.0 | BSD-3 | N/A (test) | Mock library |
| AwesomeAssertions | 9.5.0 | MIT | N/A (test) | OSS fork of FluentAssertions |
| coverlet.collector | 10.0.1 | MIT | N/A (test) | Code coverage |

### 14.3 Appendix C: Glossary

| اصطلاح | تعریف |
|---|---|
| AOT | Ahead-Of-Time compilation، کامپایل کد به native binary در زمان build |
| Trimming | حذف کدهای unused در زمان publish برای کاهش حجم |
| CompiledBindings | تبدیل XAML bindings به کد compileشده به جای runtime reflection |
| P/Invoke | Platform Invocation Services، فراخوانی توابع native از C# |
| DI Container | Dependency Injection Container، مدیریت تزریق وابستگی‌ها |
| State Pattern | الگوی طراحی که در آن هر state یک کلاس مجزا دارد |
| Onion Architecture | معماری لایه‌ای با تمرکز روی Domain در مرکز |
| LaunchAgent | macOS daemon management با launchd |
| systemd --user | Linux user-level service management |
| Native Notification | استفاده از notification center بومی سیستم‌عامل |
| Global Hook | دریافت رویدادهای input در سطح کل سیستم |
| Idle Time | زمان بدون فعالیت کیبورد یا ماوس کاربر |
| Cycle Counter | شمارش تعداد سشن‌های focus تکمیل‌شده برای long break |
| Backpressure | مدیریت فشار در producer-consumer با bounded channel |

---

*این سند بر اساس معماری و پیاده‌سازی واقعی پروژه‌ی Pomodoro تهیه شده است. نسخه‌ی فعلی شامل تمام featureهای MVP است و در زمان تهیه‌ی این سند، ۳۴ تست موفق در سه لایه‌ی پروژه اجرا شده‌اند.*
