# Последний вылет Unity — 22 октября 2026

## Сведения

- Проект: D:\Unity\Projects\Tif
- Время: примерно 10:34:37, Asia/Novosibirsk (UTC+7). Определено по имени каталога аварии: 03:34:37 UTC; точного времени исключения в строках лога нет.
- Unity: 6000.5.9f1 (b57deb96f08d), Release, x64.
- ОС: Windows 11 (10.0.26100), CoreSingleLanguage.
- Физическая память по логу запуска: 24029 MB.
- Графика: Intel(R) Arc(TM) Graphics, Direct3D 12, feature level 12.2.
- Версия драйвера по логу: 32.0.101.6078.
- Последняя указанная загруженная сцена: Assets/Scenes/Act 2 Recover.unity.

## Что зафиксировано

Unity сообщил «Could not allocate memory: System out of memory!», затем «Fatal Error!» и «Crash!!!».
Не удалось выделить 4 133 334 байта (примерно 3,94 MiB), выравнивание 16, MemoryLabel: Serialization.
Место ошибки выделения: Allocators.h:156; рядом указан MemoryManager.cpp:1603.

Стек ошибки памяти проходит через загрузку ресурсов редактора и иконок:
EditorResources.Load → EditorGUIUtility.LoadIcon → PresetSelector.Style..cctor → DrawEditorHeaderItems.
Финальный стек аварии также проходит через отрисовку заголовка Inspector и расчёт layout UI Toolkit.
Перед «Crash!!!» записано предупреждение о незавершённом либо рекурсивном OnGUI rendering.

Подтверждённый непосредственный сбой — отказ выделения памяти. По одному Editor.log нельзя установить первопричину исчерпания памяти, наличие утечки, состояние файла подкачки или конкретный виновный скрипт/пакет.
Показатель reserved: 254912110704B из диагностики аллокаторов нельзя считать измерением фактически занятой RAM процессом.
Последовательность сообщений не доказывает, что загрузка сцены или предупреждение OnGUI стали первопричиной.
Дамп crash.dmp в рамках этого отчёта не анализировался.

## Исходные файлы

- Editor.log: C:\Users\Broke\AppData\Local\Temp\Unity\Editor\Crashes\Crash_2026-10-22_033437093\Editor.log
- Дамп: C:\Users\Broke\AppData\Local\Temp\Unity\Editor\Crashes\Crash_2026-10-22_033437093\crash.dmp
- Обычный C:\Users\Broke\AppData\Local\Unity\Editor\Editor.log после перезапуска уже относится к новой сессии.

Ниже сохранены существенные фрагменты аварийного лога. Командная строка запуска и токены авторизации в отчёт не включены.

## Сообщения перед аварией и ошибка памяти

```text
Loaded scene 'Assets/Scenes/Act 2 Recover.unity'
	Deserialize:            149.770 ms
	Integration:            507.596 ms
	Integration of assets:  3.125 ms
	Thread Wait Time:       -0.016 ms
	Total Operation Time:   660.474 ms
Unloading 1609 unused Assets / (44.3 MB). Loaded Objects now: 25483.
Memory consumption went from 1.55 GB to 1.51 GB.
Total: 39.604700 ms (FindLiveObjects: 2.922100 ms CreateObjectMapping: 4.561600 ms MarkObjects: 29.328900 ms  DeleteObjects: 2.788900 ms)

[Licensing::Client] Error: Code 404 while processing request (status: Found 0 entitlement groups and 0 free entitlements matching requested entitlement ids)
[Licensing::Client] Error: Code 404 while processing request (status: Found 0 entitlement groups and 0 free entitlements matching requested entitlement ids)
[Licensing::Client] Successfully resolved entitlement details
[Licensing::Client] Successfully resolved entitlement details
[INFO ] [relay] log Unity client connected with ID: editor-3660-1ggzbe (relay 1.0.12-build.99)
[INFO ] [relay] connection.established clientId=editor-3660-1ggzbe port=9001
Could not allocate memory: System out of memory!
Trying to allocate: 4133334B with 16 alignment. MemoryLabel: Serialization
Allocation happened at: Line:156 in C:\build\output\unity\unity\Modules\NativeKernel\Include\NativeKernel\Core\Containers\Allocators.h
Memory overview

[ ALLOC_MEMORYPROFILER ] used: 3725192B &s| reserved: 254912110704B 
[ ALLOC_TEMP_TLS ] used: 184B &s| reserved: 254912110704B 
[ ALLOC_DEFAULT ] used: 1031926960B &s| reserved: 254912110704B 
[ ALLOC_TEMP_JOB_1_FRAME ] used: 0B &s| reserved: 254912110704B 
[ ALLOC_TEMP_JOB_2_FRAMES ] used: 0B &s| reserved: 254912110704B 
[ ALLOC_TEMP_JOB_4_FRAMES (JobTemp) ] used: 117959B &s| reserved: 254912110704B 
[ ALLOC_TEMP_JOB_ASYNC (Background) ] used: 174B &s| reserved: 254912110704B 
[ ALLOC_GFX ] used: 498170768B &s| reserved: 254912110704B 
[ ALLOC_CACHEOBJECTS ] used: 73753972B &s| reserved: 254912110704B 
[ ALLOC_TYPETREE ] used: 3009368B &s| reserved: 254912110704B 
[ ALLOC_PROFILER ] used: 1423496B &s| reserved: 254912110704B 
[ ALLOC_PROFILER_EDITOR ] used: 0B &s| reserved: 254912110704B 
Could not allocate memory: System out of memory!
Trying to allocate: 4133334B with 16 alignment. MemoryLabel: Serialization
Allocation happened at: Line:156 in C:\build\output\unity\unity\Modules\NativeKernel\Include\NativeKernel\Core\Containers\Allocators.h
Memory overview

[ ALLOC_MEMORYPROFILER ] used: 3725192B &s| reserved: 254912110704B 
[ ALLOC_TEMP_TLS ] used: 184B &s| reserved: 254912110704B 
[ ALLOC_DEFAULT ] used: 1031926960B &s| reserved: 254912110704B 
[ ALLOC_TEMP_JOB_1_FRAME ] used: 0B &s| reserved: 254912110704B 
[ ALLOC_TEMP_JOB_2_FRAMES ] used: 0B &s| reserved: 254912110704B 
[ ALLOC_TEMP_JOB_4_FRAMES (JobTemp) ] used: 117959B &s| reserved: 254912110704B 
[ ALLOC_TEMP_JOB_ASYNC (Background) ] used: 174B &s| reserved: 254912110704B 
[ ALLOC_GFX ] used: 498170768B &s| reserved: 254912110704B 
[ ALLOC_CACHEOBJECTS ] used: 73753972B &s| reserved: 254912110704B 
[ ALLOC_TYPETREE ] used: 3009368B &s| reserved: 254912110704B 
[ ALLOC_PROFILER ] used: 1423496B &s| reserved: 254912110704B 
[ ALLOC_PROFILER_EDITOR ] used: 0B &s| reserved: 254912110704B 
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace ()
UnityEditor.Experimental.EditorResources:Load (string,System.Type)
UnityEditor.EditorGUIUtility:Load (string,System.Type)
UnityEditor.EditorGUIUtility:Load (string)
UnityEditor.EditorGUIUtility:InnerLoadGeneratedIconOrNormalIcon (string)
UnityEditor.EditorGUIUtility:LoadGeneratedIconOrNormalIcon (string)
UnityEditor.EditorGUIUtility:LoadIconForSkin (string,int)
UnityEditor.EditorGUIUtility:LoadIcon (string)
UnityEditor.EditorGUIUtility:LoadIconRequired (string)
UnityEditor.EditorGUIUtility:IconContent (string,string,bool)
UnityEditor.EditorGUIUtility:IconContent (string)
UnityEditor.Presets.PresetSelector/Style:.cctor ()
UnityEditor.EditorGUIUtility:DrawEditorHeaderItems (UnityEngine.Rect,UnityEngine.Object[],single)
UnityEditor.Editor:DrawHeaderHelpAndSettingsGUI (UnityEngine.Rect)
UnityEditor.Editor:DrawHeaderGUI (UnityEditor.Editor,string,single)
UnityEditor.Editor:DrawHeaderGUI (UnityEditor.Editor,string)
UnityEditor.Editor:OnHeaderGUI ()
UnityEditor.Editor:DrawHeader ()
UnityEditor.UIElements.EditorElement:DrawEditorLargeHeader (UnityEditor.Editor[],bool&)
UnityEditor.UIElements.EditorElement:DrawEditorHeader (UnityEditor.Editor[],UnityEngine.Object,bool&)
UnityEditor.UIElements.EditorElement:HeaderOnGUI ()
UnityEngine.UIElements.IMGUIContainer:DoOnGUI (UnityEngine.Event,UnityEngine.Matrix4x4,UnityEngine.Rect,bool,UnityEngine.Rect,System.Action,bool)
UnityEngine.UIElements.IMGUIContainer:DoMeasure (single,UnityEngine.UIElements.VisualElement/MeasureMode,single,UnityEngine.UIElements.VisualElement/MeasureMode)
UnityEngine.UIElements.VisualElement:Measure (UnityEngine.UIElements.VisualElement,UnityEngine.UIElements.Layout.LayoutNode&,single,UnityEngine.UIElements.Layout.LayoutMeasureMode,single,UnityEngine.UIElements.Layout.LayoutMeasureMode,UnityEngine.UIElements.Layout.LayoutSize&)
UnityEngine.UIElements.Layout.LayoutDelegates:InvokeMeasureFunction (UnityEngine.UIElements.Layout.LayoutNode&,single,UnityEngine.UIElements.Layout.LayoutMeasureMode,single,UnityEngine.UIElements.Layout.LayoutMeasureMode,intptr&,UnityEngine.UIElements.Layout.LayoutSize&)
UnityEngine.UIElements.Layout.LayoutProcessorNative:UnityEngine.UIElements.Layout.ILayoutProcessor.CalculateLayout (UnityEngine.UIElements.Layout.LayoutNode,single,single,UnityEngine.UIElements.Layout.LayoutDirection)
UnityEngine.UIElements.Layout.LayoutProcessor:CalculateLayout (UnityEngine.UIElements.Layout.LayoutNode,single,single,UnityEngine.UIElements.Layout.LayoutDirection)
UnityEngine.UIElements.Layout.LayoutNode:CalculateLayout (single,single)
UnityEngine.UIElements.VisualTreeLayoutUpdater:Update ()
UnityEngine.UIElements.VisualTreeUpdater:UpdateVisualTreePhase (UnityEngine.UIElements.VisualTreeUpdatePhase)
UnityEngine.UIElements.Panel:UpdateForRepaint ()
UnityEditor.UIElements.EditorPanel:UpdateForRepaint ()
UnityEngine.UIElements.Panel:Repaint (UnityEngine.Event)
UnityEditor.UIElements.EditorEventDispatchUtility:DoDispatch (UnityEngine.UIElements.BaseVisualElementPanel,UnityEngine.Event)
UnityEditor.UIElements.DefaultWindowBackend:ProcessEvent (UnityEngine.Event)
UnityEditor.GUIView:ProcessEvent (UnityEngine.EntityId,intptr,bool&)

[C:\build\output\unity\unity\Modules\NativeKernel\Source\Allocator\MemoryManager.cpp line 1603]


# Fatal Error!

Could not allocate memory: System out of memory!
Trying to allocate: 4133334B with 16 alignment. MemoryLabel: Serialization
Allocation happened at: Line:156 in C:\build\output\unity\unity\Modules\NativeKernel\Include\NativeKernel\Core\Containers\Allocators.h
Memory overview

[ ALLOC_MEMORYPROFILER ] used: 3725192B &s| reserved: 254912110704B 
[ ALLOC_TEMP_TLS ] used: 184B &s| reserved: 254912110704B 
[ ALLOC_DEFAULT ] used: 1031926960B &s| reserved: 254912110704B 
[ ALLOC_TEMP_JOB_1_FRAME ] used: 0B &s| reserved: 254912110704B 
[ ALLOC_TEMP_JOB_2_FRAMES ] used: 0B &s| reserved: 254912110704B 
[ ALLOC_TEMP_JOB_4_FRAMES (JobTemp) ] used: 117959B &s| reserved: 254912110704B 
[ ALLOC_TEMP_JOB_ASYNC (Background) ] used: 174B &s| reserved: 254912110704B 
[ ALLOC_GFX ] used: 498170768B &s| reserved: 254912110704B 
[ ALLOC_CACHEOBJECTS ] used: 73753972B &s| reserved: 254912110704B 
[ ALLOC_TYPETREE ] used: 3009368B &s| reserved: 254912110704B 
[ ALLOC_PROFILER ] used: 1423496B &s| reserved: 254912110704B 
[ ALLOC_PROFILER_EDITOR ] used: 0B &s| reserved: 254912110704B 

UnityEditor.GUIView:ProcessEvent (UnityEngine.EntityId,intptr,bool&)


| Quit |

Scanning for USB devices : 1.547ms
Scanning for USB devices : 1.709ms
GUI Window tried to begin rendering while something else had not finished rendering! Either you have a recursive OnGUI rendering, or the previous OnGUI did not clean up properly.
Crash!!!
```

## Финальный стек аварии

```text
0x0000022E26291076 ((<unknown>)) (function-name not available)
0x0000023025ACA289 (Mono JIT Code) UnityEditor.EditorGUIUtility:DrawEditorHeaderItems (UnityEngine.Rect,UnityEngine.Object[],single)
0x0000023025AC98A3 (Mono JIT Code) UnityEditor.Editor:DrawHeaderHelpAndSettingsGUI (UnityEngine.Rect)
0x0000023025AC6FFD (Mono JIT Code) UnityEditor.Editor:DrawHeaderGUI (UnityEditor.Editor,string,single)
0x0000023025AC6813 (Mono JIT Code) UnityEditor.Editor:DrawHeaderGUI (UnityEditor.Editor,string)
0x0000023025AC64C3 (Mono JIT Code) UnityEditor.Editor:OnHeaderGUI ()
0x0000023025AC6041 (Mono JIT Code) UnityEditor.Editor:DrawHeader ()
0x0000023025AC5DFB (Mono JIT Code) UnityEditor.UIElements.EditorElement:DrawEditorLargeHeader (UnityEditor.Editor[],bool&)
0x0000023025AC59A3 (Mono JIT Code) UnityEditor.UIElements.EditorElement:DrawEditorHeader (UnityEditor.Editor[],UnityEngine.Object,bool&)
0x0000023025AC4A4B (Mono JIT Code) UnityEditor.UIElements.EditorElement:HeaderOnGUI ()
0x0000022FFBA2080F (Mono JIT Code) UnityEngine.UIElements.IMGUIContainer:DoOnGUI (UnityEngine.Event,UnityEngine.Matrix4x4,UnityEngine.Rect,bool,UnityEngine.Rect,System.Action,bool)
0x0000023025AB8BEB (Mono JIT Code) UnityEngine.UIElements.IMGUIContainer:DoMeasure (single,UnityEngine.UIElements.VisualElement/MeasureMode,single,UnityEngine.UIElements.VisualElement/MeasureMode)
0x0000023025A2758D (Mono JIT Code) UnityEngine.UIElements.VisualElement:Measure (UnityEngine.UIElements.VisualElement,UnityEngine.UIElements.Layout.LayoutNode&,single,UnityEngine.UIElements.Layout.LayoutMeasureMode,single,UnityEngine.UIElements.Layout.LayoutMeasureMode,UnityEngine.UIElements.Layout.LayoutSize&)
0x0000023025A279B3 (Mono JIT Code) (wrapper delegate-invoke) <Module>:invoke_void_VisualElement_LayoutNode&_single_LayoutMeasureMode_single_LayoutMeasureMode_LayoutSize& (UnityEngine.UIElements.VisualElement,UnityEngine.UIElements.Layout.LayoutNode&,single,UnityEngine.UIElements.Layout.LayoutMeasureMode,single,UnityEngine.UIElements.Layout.LayoutMeasureMode,UnityEngine.UIElements.Layout.LayoutSize&)
0x0000023025A26B50 (Mono JIT Code) UnityEngine.UIElements.Layout.LayoutDelegates:InvokeMeasureFunction (UnityEngine.UIElements.Layout.LayoutNode&,single,UnityEngine.UIElements.Layout.LayoutMeasureMode,single,UnityEngine.UIElements.Layout.LayoutMeasureMode,intptr&,UnityEngine.UIElements.Layout.LayoutSize&)
0x00000230266EF8BB (Mono JIT Code) (wrapper native-to-managed) UnityEngine.UIElements.Layout.LayoutDelegates:InvokeMeasureFunction (UnityEngine.UIElements.Layout.LayoutNode&,single,UnityEngine.UIElements.Layout.LayoutMeasureMode,single,UnityEngine.UIElements.Layout.LayoutMeasureMode,intptr&,UnityEngine.UIElements.Layout.LayoutSize&)
0x00007FFD033BA9F9 (Unity) UIToolkit::LayoutNative::NodeWithMeasureFuncSetMeasuredDimensions
0x00007FFD033B58E0 (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B4EF4 (Unity) UIToolkit::LayoutNative::NodeComputeFlexBasisForChild
0x00007FFD033B63CB (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B4EF4 (Unity) UIToolkit::LayoutNative::NodeComputeFlexBasisForChild
0x00007FFD033B63CB (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B4EF4 (Unity) UIToolkit::LayoutNative::NodeComputeFlexBasisForChild
0x00007FFD033B63CB (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B4EF4 (Unity) UIToolkit::LayoutNative::NodeComputeFlexBasisForChild
0x00007FFD033B63CB (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B4EF4 (Unity) UIToolkit::LayoutNative::NodeComputeFlexBasisForChild
0x00007FFD033B63CB (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B7933 (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B4EF4 (Unity) UIToolkit::LayoutNative::NodeComputeFlexBasisForChild
0x00007FFD033B63CB (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B4EF4 (Unity) UIToolkit::LayoutNative::NodeComputeFlexBasisForChild
0x00007FFD033B63CB (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B8C2E (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B8C2E (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B8C2E (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B401A (Unity) UIToolkit::LayoutNative::NodeAbsoluteLayoutChild
0x00007FFD033B9933 (Unity) UIToolkit::LayoutNative::NodeLayoutImpl
0x00007FFD033B35E1 (Unity) UIToolkit::LayoutNative::LayoutNodeInternal
0x00007FFD033B2B02 (Unity) UIToolkit::LayoutNative::CalculateLayout
0x00007FFD03372BA5 (Unity) LayoutNative_CUSTOM_CalculateLayout
0x00000230266F00B3 (Mono JIT Code) (wrapper managed-to-native) UnityEngine.UIElements.Layout.LayoutNative:CalculateLayout (intptr,single,single,int,intptr,intptr)
0x00000230266EFD03 (Mono JIT Code) UnityEngine.UIElements.Layout.LayoutProcessorNative:UnityEngine.UIElements.Layout.ILayoutProcessor.CalculateLayout (UnityEngine.UIElements.Layout.LayoutNode,single,single,UnityEngine.UIElements.Layout.LayoutDirection)
0x00000230266EFC45 (Mono JIT Code) UnityEngine.UIElements.Layout.LayoutProcessor:CalculateLayout (UnityEngine.UIElements.Layout.LayoutNode,single,single,UnityEngine.UIElements.Layout.LayoutDirection)
0x00000230266EF1D3 (Mono JIT Code) UnityEngine.UIElements.Layout.LayoutNode:CalculateLayout (single,single)
0x00000230266ED363 (Mono JIT Code) UnityEngine.UIElements.VisualTreeLayoutUpdater:Update ()
0x0000022EF312D57E (Mono JIT Code) UnityEngine.UIElements.VisualTreeUpdater:UpdateVisualTreePhase (UnityEngine.UIElements.VisualTreeUpdatePhase)
0x00000230266D0E3B (Mono JIT Code) UnityEngine.UIElements.Panel:UpdateForRepaint ()
0x00000230266D0803 (Mono JIT Code) UnityEditor.UIElements.EditorPanel:UpdateForRepaint ()
0x0000022FFBA0F20F (Mono JIT Code) UnityEngine.UIElements.Panel:Repaint (UnityEngine.Event)
0x0000022FFBA0DF6D (Mono JIT Code) UnityEditor.UIElements.EditorEventDispatchUtility:DoDispatch (UnityEngine.UIElements.BaseVisualElementPanel,UnityEngine.Event)
0x0000022FFBA0DA7B (Mono JIT Code) UnityEditor.UIElements.DefaultWindowBackend:ProcessEvent (UnityEngine.Event)
0x0000022FFBA0C70A (Mono JIT Code) UnityEditor.GUIView:ProcessEvent (UnityEngine.EntityId,intptr,bool&)
0x0000022FFBA0C94E (Mono JIT Code) (wrapper runtime-invoke) <Module>:runtime_invoke_void_EntityId_intptr_intptr& (object,intptr,intptr,intptr)
0x00007FFCF60C755E (mono-2.0-bdwgc) [C:\build\output\Unity-Technologies\mono\mono\mini\mini-runtime.c:3445] mono_jit_runtime_invoke 
0x00007FFCF60091D4 (mono-2.0-bdwgc) [C:\build\output\Unity-Technologies\mono\mono\metadata\object.c:3068] do_runtime_invoke 
0x00007FFCF60092C0 (mono-2.0-bdwgc) [C:\build\output\Unity-Technologies\mono\mono\metadata\object.c:3115] mono_runtime_invoke 
0x00007FFD04A939E5 (Unity) scripting_method_invoke
0x00007FFD04ABBC86 (Unity) ScriptingInvocation::Invoke
0x00007FFD04AB6A45 (Unity) ScriptingInvocation::Invoke<void>
0x00007FFD02739E95 (Unity) Scripting::UnityEditor::GUIViewProxy::ProcessEvent
0x00007FFD02BA8D7D (Unity) GUIView::ProcessRetainedMode
0x00007FFD02A01A56 (Unity) GUIView::OnInputEvent
0x00007FFD02BA8CA3 (Unity) GUIView::ProcessInputEventFromAPI
0x00007FFD02BA8B31 (Unity) GUIView::ProcessInputEvent
0x00007FFD029FD1B1 (Unity) GUIView::DoPaint
0x00007FFD02BADCBA (Unity) NativeWindowManager::ExecuteRepaintOnViews
0x00007FFD0314763C (Unity) PlayerLoopController::InternalUpdateScene
0x00007FFD0314975A (Unity) PlayerLoopController::UpdateSceneIfNeededFromMainLoop
0x00007FFD03161254 (Unity) Application::TickTimer
0x00007FFD02A40CAC (Unity) MainMessageLoop
0x00007FFD02A498D1 (Unity) UnityMain
0x00007FF753902F2A (Unity) __scrt_common_main_seh
0x00007FFDB8ECE8D7 (KERNEL32) BaseThreadInitThunk
0x00007FFDBA35C34C (ntdll) RtlUserThreadStart

========== END OF STACKTRACE ===========

A crash has been intercepted by the crash handler. For call stack and other details, see the latest crash report generated in:
 * C:/Users/Broke/AppData/Local/Temp/Unity/Editor/Crashes
```
