using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class HierarchyComponentIcons
{
    private const string ColumnId = "GameObject/Components";
    private const string WindowTypeName = "Unity.Hierarchy.Editor.HierarchyWindow";
    private const string ViewTypeName = "Unity.Hierarchy.HierarchyView";
    private const string ColumnDescriptorTypeName = "Unity.Hierarchy.HierarchyViewColumnDescriptor";
    private const string CellDescriptorTypeName = "Unity.Hierarchy.HierarchyViewCellDescriptor";
    private const string HandlerTypeName = "Unity.Hierarchy.Editor.HierarchyGameObjectHandler";
    private const int LeftAlignment = 0;
    private const int CenterAlignment = 1;
    private const int RightAlignment = 2;
    private const float IconSize = 14f;
    private const float IconSpacing = 2f;
    private const string SettingsPath = "Preferences/Hierarchy Component Icons";
    private const string SettingsKeyPrefix = "PointOfCollapse.HierarchyComponentIcons.";
    private const string SettingsInitializedKey = "Initialized";
    private const string IgnoredTypesKey = "IgnoredTypes";
    private const string AlignmentKey = "Alignment";

    private static readonly string[] AlignmentOptions = { "Left", "Center", "Right" };

    private static readonly Type WindowType;
    private static readonly Type ViewType;
    private static readonly Type ColumnDescriptorType;
    private static readonly Type CellDescriptorType;
    private static readonly Type HandlerType;
    private static readonly FieldInfo WindowsField;
    private static readonly FieldInfo ColumnDescriptorsField;
    private static readonly FieldInfo CellDescriptorsField;
    private static readonly FieldInfo ViewField;
    private static readonly FieldInfo ViewStateField;
    private static readonly FieldInfo CellBindField;
    private static readonly MethodInfo CellDefaultValueSetter;
    private static readonly MethodInfo GetGameObjectMethod;
    private static readonly MethodInfo SetColumnDescriptorsMethod;
    private static readonly Delegate CellBindDelegate;
    private static readonly List<Type> ComponentTypes = new List<Type>();
    private static HashSet<string> IgnoredTypeKeys;
    private static Vector2 SettingsScrollPosition;
    private static string SettingsTypeSearch = string.Empty;
    private static GUIStyle SearchResultLabelStyle;
    private static GUIStyle TypeIconStyle;
    private static readonly Dictionary<Type, Texture> TypeIcons = new Dictionary<Type, Texture>();

    static HierarchyComponentIcons()
    {
        WindowType = FindType(WindowTypeName);
        ViewType = FindType(ViewTypeName);
        ColumnDescriptorType = FindType(ColumnDescriptorTypeName);
        CellDescriptorType = FindType(CellDescriptorTypeName);
        HandlerType = FindType(HandlerTypeName);

        if (WindowType == null || ViewType == null || ColumnDescriptorType == null ||
            CellDescriptorType == null || HandlerType == null)
        {
            return;
        }

        WindowsField = WindowType.GetField("s_HierarchyWindows", BindingFlags.Static | BindingFlags.NonPublic);
        ColumnDescriptorsField = WindowType.GetField("m_ColumnDescriptors", BindingFlags.Instance | BindingFlags.NonPublic);
        CellDescriptorsField = WindowType.GetField("m_CellDescriptors", BindingFlags.Instance | BindingFlags.NonPublic);
        ViewField = WindowType.GetField("m_HierarchyView", BindingFlags.Instance | BindingFlags.NonPublic);
        ViewStateField = WindowType.GetField("m_ViewState", BindingFlags.Instance | BindingFlags.NonPublic);
        CellBindField = CellDescriptorType.GetField("BindCell", BindingFlags.Instance | BindingFlags.Public);
        CellDefaultValueSetter = FindType(ViewTypeName + "Cell")?.GetProperty("IsDefaultValue", BindingFlags.Instance | BindingFlags.Public)?.GetSetMethod();
        GetGameObjectMethod = FindType("Unity.Hierarchy.Editor.HierarchyWindowColumnUtility")?.GetMethod(
            "GetGameObject",
            BindingFlags.Static | BindingFlags.Public);
        SetColumnDescriptorsMethod = ViewType.GetMethod(
            "SetColumnDescriptors",
            BindingFlags.Instance | BindingFlags.Public);

        if (CellBindField != null)
        {
            var callback = typeof(HierarchyComponentIcons).GetMethod(
                nameof(BindCell),
                BindingFlags.Static | BindingFlags.NonPublic);
            var cellParameter = Expression.Parameter(CellBindField.FieldType.GetGenericArguments()[0], "cell");
            var bindCall = Expression.Call(
                callback,
                Expression.Convert(cellParameter, typeof(VisualElement)));
            CellBindDelegate = Expression.Lambda(
                CellBindField.FieldType,
                bindCall,
                cellParameter).Compile();
        }

        var initializingView = WindowType.GetEvent(
            "InitializingView",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        initializingView?.AddEventHandler(null, (Action<VisualElement>)OnHierarchyViewInitializing);

        EditorApplication.delayCall += RegisterOnAllWindows;
    }

    private static void OnHierarchyViewInitializing(VisualElement _)
    {
        EditorApplication.delayCall += RegisterOnAllWindows;
    }

    private static void RegisterOnAllWindows()
    {
        if (WindowsField == null || ColumnDescriptorsField == null || CellDescriptorsField == null ||
            ViewField == null || ViewStateField == null || SetColumnDescriptorsMethod == null ||
            CellBindDelegate == null)
        {
            return;
        }

        var windows = WindowsField.GetValue(null) as IEnumerable;
        if (windows == null)
        {
            return;
        }

        foreach (var window in windows)
        {
            RegisterWindow(window);
        }
    }

    private static void RegisterWindow(object window)
    {
        var columnDescriptors = ColumnDescriptorsField.GetValue(window) as IList;
        var cellDescriptors = CellDescriptorsField.GetValue(window) as IList;
        var view = ViewField.GetValue(window);
        var viewState = ViewStateField.GetValue(window);
        if (columnDescriptors == null || cellDescriptors == null || view == null)
        {
            return;
        }

        foreach (var descriptor in columnDescriptors)
        {
            var id = descriptor.GetType().GetField("Id")?.GetValue(descriptor) as string;
            if (id == ColumnId)
            {
                return;
            }
        }

        var columnDescriptor = CreateInstance(ColumnDescriptorType, ColumnId);
        SetField(columnDescriptor, "Title", "Components");
        SetField(columnDescriptor, "Tooltip", "Show all component icons");
        SetField(columnDescriptor, "DefaultPriority", 1000);
        SetField(columnDescriptor, "DefaultWidth", 180);
        SetField(columnDescriptor, "DefaultVisibility", true);

        var cellDescriptor = CreateInstance(
            CellDescriptorType,
            ColumnId,
            HandlerType);
        SetField(cellDescriptor, "BindCell", CellBindDelegate);
        SetField(cellDescriptor, "ClearCellContent", true);

        columnDescriptors.Add(columnDescriptor);
        cellDescriptors.Add(cellDescriptor);

        SetColumnDescriptorsMethod.Invoke(
            view,
            new[] { columnDescriptors, cellDescriptors, viewState });
    }

    private static void RefreshRegisteredWindows()
    {
        if (WindowsField == null || ColumnDescriptorsField == null || CellDescriptorsField == null ||
            ViewField == null || ViewStateField == null || SetColumnDescriptorsMethod == null)
        {
            return;
        }

        var windows = WindowsField.GetValue(null) as IEnumerable;
        if (windows == null)
        {
            return;
        }

        foreach (var window in windows)
        {
            var columnDescriptors = ColumnDescriptorsField.GetValue(window) as IList;
            var cellDescriptors = CellDescriptorsField.GetValue(window) as IList;
            var view = ViewField.GetValue(window);
            var viewState = ViewStateField.GetValue(window);
            if (columnDescriptors == null || cellDescriptors == null || view == null)
            {
                continue;
            }

            SetColumnDescriptorsMethod.Invoke(
                view,
                new[] { columnDescriptors, cellDescriptors, viewState });
        }

        EditorApplication.RepaintHierarchyWindow();
    }

    private static void BindCell(VisualElement cell)
    {
        cell.Clear();

        var iconRow = new VisualElement
        {
            name = "component-icons"
        };
        iconRow.style.flexDirection = FlexDirection.Row;
        iconRow.style.alignItems = Align.Center;
        iconRow.style.justifyContent = GetIconAlignment();
        iconRow.style.flexGrow = 1;

        var gameObject = GetGameObjectMethod?.Invoke(null, new object[] { cell }) as GameObject;
        if (gameObject == null)
        {
            cell.Add(iconRow);
            SetDefaultValue(cell, false);
            return;
        }

        foreach (var component in gameObject.GetComponents<Component>())
        {
            if (component == null)
            {
                continue;
            }

            var icon = GetComponentIcon(component);
            if (icon == null)
            {
                continue;
            }

            var iconElement = new Image
            {
                image = icon,
                tooltip = component.GetType().Name
            };
            iconElement.style.width = IconSize;
            iconElement.style.height = IconSize;
            iconElement.style.marginLeft = IconSpacing;
            iconRow.Add(iconElement);
        }

        cell.Add(iconRow);
        SetDefaultValue(cell, false);
    }

    private static Texture GetComponentIcon(Component component)
    {
        var componentType = component.GetType();

        if (IsDefaultIgnoredType(componentType) || IsIgnoredType(componentType))
        {
            return null;
        }

        if (componentType.IsSubclassOf(typeof(MonoBehaviour)) && !HasCustomScriptIcon(component))
        {
            return null;
        }

        return EditorGUIUtility.ObjectContent(component, componentType).image;
    }

    private static bool HasCustomScriptIcon(Component component)
    {
        var monoScript = MonoScript.FromMonoBehaviour(component as MonoBehaviour);
        if (monoScript == null)
        {
            return false;
        }

        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(monoScript)) as MonoImporter;
        return importer != null && importer.GetIcon() != null;
    }

    [SettingsProvider]
    private static SettingsProvider CreateSettingsProvider()
    {
        var provider = new SettingsProvider(SettingsPath, SettingsScope.User)
        {
            label = "Hierarchy Component Icons",
            keywords = new HashSet<string>(new[] { "Hierarchy", "Component", "Icon", "TypeCache" })
        };
        provider.activateHandler = (searchContext, rootElement) =>
        {
            EnsureSettingsInitialized();
            RefreshComponentTypes();
        };
        provider.guiHandler = _ => DrawSettingsGUI();
        return provider;
    }

    private static void DrawSettingsGUI()
    {
        EnsureSettingsInitialized();
        RefreshComponentTypes();

        var alignment = GetAlignment();
        var newAlignment = EditorGUILayout.Popup("Icon alignment", alignment, AlignmentOptions);
        if (newAlignment != alignment)
        {
            SetAlignment(newAlignment);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Ignored component types", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "All component icons are shown by default. Transform and RectTransform are ignored by default.",
            MessageType.Info);
        EditorGUILayout.LabelField("Add ignored type", EditorStyles.boldLabel);
        SettingsTypeSearch = EditorGUILayout.TextField("Search types", SettingsTypeSearch);

        var availableTypes = GetAvailableTypes();
        if (string.IsNullOrWhiteSpace(SettingsTypeSearch))
        {
            EditorGUILayout.LabelField("Type a name to find a component.", EditorStyles.miniLabel);
        }
        else if (availableTypes.Count == 0)
        {
            EditorGUILayout.LabelField("No matching component types.", EditorStyles.miniLabel);
        }
        else
        {
            var resultCount = Mathf.Min(availableTypes.Count, 20);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            for (var i = 0; i < resultCount; i++)
            {
                var componentType = availableTypes[i];
                var typeName = componentType.FullName ?? componentType.Name;
                EditorGUILayout.BeginHorizontal();
                DrawTypeIcon(componentType);
                EditorGUILayout.SelectableLabel(
                    HighlightSearchMatches(typeName),
                    GetSearchResultLabelStyle(),
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Add", GUILayout.Width(60f)))
                {
                    SetTypeIgnored(componentType, true);
                }

                EditorGUILayout.EndHorizontal();
            }

            if (availableTypes.Count > resultCount)
            {
                EditorGUILayout.LabelField("Refine the search to see more types.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("Reset defaults"))
        {
            ResetSettings();
        }

        EditorGUILayout.LabelField("Added filters", EditorStyles.boldLabel);
        SettingsScrollPosition = EditorGUILayout.BeginScrollView(SettingsScrollPosition);
        var ignoredTypes = new List<string>(GetIgnoredTypes());
        ignoredTypes.Sort(StringComparer.Ordinal);
        foreach (var typeKey in ignoredTypes)
        {
            var componentType = FindComponentType(typeKey);
            var typeName = componentType == null
                ? typeKey
                : componentType.FullName ?? componentType.Name;

            EditorGUILayout.BeginHorizontal();
            DrawTypeIcon(componentType);
            EditorGUILayout.SelectableLabel(
                componentType == null ? typeName : ColorizeNamespace(typeName),
                GetSearchResultLabelStyle(),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button("Remove", GUILayout.Width(70f)))
            {
                GetIgnoredTypes().Remove(typeKey);
                SaveIgnoredTypes();
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private static List<Type> GetAvailableTypes()
    {
        var availableTypes = new List<Type>();
        foreach (var componentType in ComponentTypes)
        {
            if (IsDefaultIgnoredType(componentType) || IsIgnoredType(componentType))
            {
                continue;
            }

            var typeName = componentType.FullName ?? componentType.Name;
            if (!MatchesTypeSearch(typeName))
            {
                continue;
            }

            availableTypes.Add(componentType);
        }

        return availableTypes;
    }

    private static bool MatchesTypeSearch(string typeName)
    {
        var searchParts = GetSearchParts();
        foreach (var searchPart in searchParts)
        {
            if (typeName.IndexOf(searchPart, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static string[] GetSearchParts()
    {
        return SettingsTypeSearch.Split(
            new[] { ' ', '\t' },
            StringSplitOptions.RemoveEmptyEntries);
    }

    private static string HighlightSearchMatches(string typeName)
    {
        var searchParts = GetSearchParts();
        if (searchParts.Length == 0)
        {
            return typeName;
        }

        var matchedCharacters = new bool[typeName.Length];
        foreach (var searchPart in searchParts)
        {
            var searchIndex = 0;
            while (searchIndex < typeName.Length)
            {
                var matchIndex = typeName.IndexOf(
                    searchPart,
                    searchIndex,
                    StringComparison.OrdinalIgnoreCase);
                if (matchIndex < 0)
                {
                    break;
                }

                for (var i = matchIndex; i < matchIndex + searchPart.Length; i++)
                {
                    matchedCharacters[i] = true;
                }

                searchIndex = matchIndex + searchPart.Length;
            }
        }

        var highlightedName = new StringBuilder(typeName.Length + 64);
        var highlightColor = EditorGUIUtility.isProSkin ? "#FFD54F" : "#9A5500";
        var namespaceColor = EditorGUIUtility.isProSkin ? "#8E8E8E" : "#666666";
        var namespaceEnd = typeName.LastIndexOf('.') + 1;
        var currentStyle = 0;
        for (var i = 0; i < typeName.Length; i++)
        {
            var style = (i < namespaceEnd ? 1 : 0) | (matchedCharacters[i] ? 2 : 0);
            if (style != currentStyle)
            {
                CloseRichTextStyle(highlightedName, currentStyle);
                OpenRichTextStyle(highlightedName, style, namespaceColor, highlightColor);
                currentStyle = style;
            }

            highlightedName.Append(typeName[i]);
        }

        CloseRichTextStyle(highlightedName, currentStyle);

        return highlightedName.ToString();
    }

    private static void OpenRichTextStyle(
        StringBuilder text,
        int style,
        string namespaceColor,
        string highlightColor)
    {
        if ((style & 1) != 0)
        {
            text.Append("<color=");
            text.Append(namespaceColor);
            text.Append(">");
        }

        if ((style & 2) != 0)
        {
            text.Append("<b><color=");
            text.Append(highlightColor);
            text.Append(">");
        }
    }

    private static void CloseRichTextStyle(StringBuilder text, int style)
    {
        if ((style & 2) != 0)
        {
            text.Append("</color></b>");
        }

        if ((style & 1) != 0)
        {
            text.Append("</color>");
        }
    }

    private static string ColorizeNamespace(string typeName)
    {
        var namespaceEnd = typeName.LastIndexOf('.') + 1;
        if (namespaceEnd <= 0)
        {
            return typeName;
        }

        var namespaceColor = EditorGUIUtility.isProSkin ? "#8E8E8E" : "#666666";
        var coloredName = new StringBuilder(typeName.Length + 32);
        coloredName.Append("<color=");
        coloredName.Append(namespaceColor);
        coloredName.Append(">");
        coloredName.Append(typeName, 0, namespaceEnd);
        coloredName.Append("</color>");
        coloredName.Append(typeName, namespaceEnd, typeName.Length - namespaceEnd);
        return coloredName.ToString();
    }

    private static GUIStyle GetSearchResultLabelStyle()
    {
        if (SearchResultLabelStyle == null)
        {
            SearchResultLabelStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true
            };
        }

        return SearchResultLabelStyle;
    }

    private static void DrawTypeIcon(Type componentType)
    {
        var icon = GetTypeIcon(componentType);
        if (icon == null)
        {
            GUILayout.Space(IconSize + IconSpacing);
            return;
        }

        if (TypeIconStyle == null)
        {
            TypeIconStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0)
            };
        }

        EditorGUILayout.LabelField(
            new GUIContent(icon, componentType == null ? string.Empty : componentType.FullName),
            TypeIconStyle,
            GUILayout.Width(IconSize),
            GUILayout.Height(EditorGUIUtility.singleLineHeight));
        GUILayout.Space(IconSpacing);
    }

    private static Texture GetTypeIcon(Type componentType)
    {
        if (componentType == null)
        {
            return null;
        }

        Texture icon;
        if (TypeIcons.TryGetValue(componentType, out icon))
        {
            return icon;
        }

        icon = EditorGUIUtility.ObjectContent(null, componentType).image;
        TypeIcons[componentType] = icon;
        return icon;
    }

    private static void RefreshComponentTypes()
    {
        if (ComponentTypes.Count > 0)
        {
            return;
        }

        foreach (var componentType in TypeCache.GetTypesDerivedFrom<Component>())
        {
            if (componentType == null ||
                componentType == typeof(Component) ||
                !typeof(Component).IsAssignableFrom(componentType) ||
                componentType.IsAbstract ||
                componentType.ContainsGenericParameters)
            {
                continue;
            }

            ComponentTypes.Add(componentType);
        }

        ComponentTypes.Sort((left, right) => string.Compare(
            left.FullName,
            right.FullName,
            StringComparison.Ordinal));
    }

    private static void EnsureSettingsInitialized()
    {
        if (EditorPrefs.GetBool(GetSettingsKey(SettingsInitializedKey), false))
        {
            return;
        }

        IgnoredTypeKeys = new HashSet<string>();
        SaveIgnoredTypes();
        EditorPrefs.SetInt(GetSettingsKey(AlignmentKey), RightAlignment);
        EditorPrefs.SetBool(GetSettingsKey(SettingsInitializedKey), true);
    }

    private static HashSet<string> GetIgnoredTypes()
    {
        EnsureSettingsInitialized();
        if (IgnoredTypeKeys != null)
        {
            return IgnoredTypeKeys;
        }

        IgnoredTypeKeys = new HashSet<string>();
        var savedTypes = EditorPrefs.GetString(GetSettingsKey(IgnoredTypesKey), string.Empty);
        foreach (var savedType in savedTypes.Split('|'))
        {
            if (!string.IsNullOrEmpty(savedType))
            {
                IgnoredTypeKeys.Add(savedType);
            }
        }

        return IgnoredTypeKeys;
    }

    private static bool IsIgnoredType(Type componentType)
    {
        return GetIgnoredTypes().Contains(GetTypeKey(componentType));
    }

    private static bool IsDefaultIgnoredType(Type componentType)
    {
        return componentType == typeof(Transform) || componentType == typeof(RectTransform);
    }

    private static Type FindComponentType(string typeKey)
    {
        foreach (var componentType in ComponentTypes)
        {
            if (GetTypeKey(componentType) == typeKey)
            {
                return componentType;
            }
        }

        return Type.GetType(typeKey);
    }

    private static void SetTypeIgnored(Type componentType, bool ignored)
    {
        var ignoredTypes = GetIgnoredTypes();
        var typeKey = GetTypeKey(componentType);
        if (ignored)
        {
            ignoredTypes.Add(typeKey);
        }
        else
        {
            ignoredTypes.Remove(typeKey);
        }

        SaveIgnoredTypes();
    }

    private static void ResetSettings()
    {
        IgnoredTypeKeys = new HashSet<string>();
        SaveIgnoredTypes();
        SetAlignment(RightAlignment);
    }

    private static int GetAlignment()
    {
        EnsureSettingsInitialized();
        return Mathf.Clamp(EditorPrefs.GetInt(GetSettingsKey(AlignmentKey), RightAlignment), LeftAlignment, RightAlignment);
    }

    private static void SetAlignment(int alignment)
    {
        EditorPrefs.SetInt(GetSettingsKey(AlignmentKey), Mathf.Clamp(alignment, LeftAlignment, RightAlignment));
        RefreshRegisteredWindows();
    }

    private static Justify GetIconAlignment()
    {
        switch (GetAlignment())
        {
            case LeftAlignment:
                return Justify.FlexStart;
            case CenterAlignment:
                return Justify.Center;
            default:
                return Justify.FlexEnd;
        }
    }

    private static void SaveIgnoredTypes()
    {
        var typeKeys = new List<string>(IgnoredTypeKeys ?? new HashSet<string>());
        typeKeys.Sort(StringComparer.Ordinal);
        EditorPrefs.SetString(GetSettingsKey(IgnoredTypesKey), string.Join("|", typeKeys));
        RefreshRegisteredWindows();
    }

    private static string GetSettingsKey(string key)
    {
        return SettingsKeyPrefix + Application.dataPath + "." + key;
    }

    private static string GetTypeKey(Type type)
    {
        return type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
    }

    private static void SetDefaultValue(VisualElement cell, bool value)
    {
        CellDefaultValueSetter?.Invoke(cell, new object[] { value });
    }

    private static void SetField(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName)?.SetValue(target, value);
    }

    private static object CreateInstance(Type type, params object[] arguments)
    {
        return Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            arguments,
            null);
    }

    private static Type FindType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        var assemblyName = fullName.StartsWith("Unity.Hierarchy.Editor.", StringComparison.Ordinal)
            ? "UnityEditor.HierarchyModule"
            : "UnityEngine.HierarchyModule";
        try
        {
            return Assembly.Load(assemblyName).GetType(fullName);
        }
        catch
        {
            return null;
        }

    }
}
