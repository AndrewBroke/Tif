# Вылет Unity — 23 октября 2026

## Сведения

- Проект: E:\Github Projects\Tif.
- Подтверждённый аварийный процесс: AssetImportWorkerHW2, фоновый процесс импорта ресурсов Unity (BatchMode: 1).
- Время аварии: примерно 14:14:09, Asia/Novosibirsk (UTC+7), по имени каталога Crash_2026-10-23_071409529. Точного времени исключения в строках лога нет.
- Последний запрос импорта перед ошибкой: 07:14:08.2929152 UTC (14:14:08 местного времени).
- Unity: 6000.5.9f1 (b57deb96f08d), Release, x64.
- ОС по логу: Windows 11 (10.0.26100), IoTEnterpriseS.
- Физическая память по логу запуска: 16091 MB. Это общий объём, а не свободная память в момент аварии.

## Что зафиксировано

В процессе импорта текстуры библиотека Crunch не смогла выделить память:

```text
C:\build\output\unity\unity\External\TextureCompressors\Crunch\crnlib\crn_mem.cpp(123): Assertion failed: "crnlib_malloc: out of memory"
```

Затем записаны Native Crash Reporting и Crash!!!. Секция Managed Stacktrace пустая; в нативном стеке многие имена функций не разрешены.

Непосредственно перед ошибкой TextureImporter начал импорт:

```text
Packages/com.unity.visualeffectgraph/ShaderGraph/VFXGraph_Unlit_TemplateThumbnail.png
GUID: 1885ac09b170b9a4387c8b35f13bd480
```

Сообщения об успешном завершении этого импорта перед аварией нет. Предыдущая текстура TextHSix.png из com.unity.dt.app-ui импортирована успешно.

Подтверждённый непосредственный сбой — отказ выделения памяти в Crunch во время работы импортера. Лог не устанавливает первопричину: объём свободной RAM, состояние файла подкачки, наличие утечки или дефект библиотеки неизвестны. Последний импортируемый файл указывает контекст сбоя, но сам по себе не доказывает повреждение ресурса или ошибку пакета Visual Effect Graph.

Этот файл подтверждает падение процесса импорта. Падение основного процесса Editor по нему отдельно не подтверждено.

## Доступность логов последней сессии

- C:\Users\Vitaly-LOQ\AppData\Local\Unity\Editor\Editor.log на момент проверки пуст (0 байт).
- Editor-prev.log в той же папке имеет дату изменения 7 октября 2026 и содержит более ранние ошибки памяти. Он не использован как доказательство причины события 23 октября.
- Более поздние каталоги Crash_2026-10-23_071916097, Crash_2026-10-23_071916908 и Crash_2026-10-23_071917200 найдены, но при проверке пусты. Поэтому установить причину самого позднего события около 14:19 по ним нельзя.
- Описанный здесь сбой около 14:14 — самый свежий найденный за 23 октября, для которого сохранился непустой аварийный лог.
- В каталоге описанного сбоя crash.dmp не найден; анализ дампа не проводился.

## Исходный файл

C:\Users\Vitaly-LOQ\AppData\Local\Temp\Unity\Editor\Crashes\Crash_2026-10-23_071409529\AssetImportWorkerHW2.log

Размер при проверке: 171627 байт. Существенные строки: 1207–1225.

Командная строка запуска, идентификаторы лицензирования и токены в отчёт не включены.

## Существенный фрагмент лога

```text
Received Import Request.
  Time of request: 2026-10-23T07:14:08.2929152Z
  Time since last request: 0.0005649 seconds.
  importAddress: GUID: 1885ac09b170b9a4387c8b35f13bd480 - Path: Packages/com.unity.visualeffectgraph/ShaderGraph/VFXGraph_Unlit_TemplateThumbnail.png
Importing 'GUID: 1885ac09b170b9a4387c8b35f13bd480 - Path: Packages/com.unity.visualeffectgraph/ShaderGraph/VFXGraph_Unlit_TemplateThumbnail.png' with importer (TextureImporter)
C:\build\output\unity\unity\External\TextureCompressors\Crunch\crnlib\crn_mem.cpp(123): Assertion failed: "crnlib_malloc: out of memory"

=================================================================
    Native Crash Reporting
=================================================================
Got a UNKNOWN while executing native code. This usually indicates
a fatal error in the mono runtime or one of the native libraries
used by your application.
=================================================================

=================================================================
    Managed Stacktrace:
=================================================================
=================================================================
Crash!!!
```
