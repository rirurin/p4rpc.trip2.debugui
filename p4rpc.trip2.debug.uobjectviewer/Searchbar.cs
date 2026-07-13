extern alias imgui;
using System.Runtime.InteropServices;
using imgui::p4rpc.trip2.ImGui;
using ImGui = imgui::p4rpc.trip2.ImGui.ImGui;

using System.Text.RegularExpressions;
using RyoTune.Reloaded;
using UE.Toolkit.Core.Types.Unreal.Factories.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer;

public abstract class SearchType
{
    public abstract string GetName();
    public abstract bool SearchMatches(string src, string tgt);
    public virtual bool PostTableError() => false;
}

public class SearchContaining : SearchType
{
    public override string GetName() => "Contains";
    public override bool SearchMatches(string src, string tgt)
        => src.Contains(tgt);
}

public class SearchWholeWord : SearchType
{
    public override string GetName() => "Whole Word";
    public override bool SearchMatches(string src, string tgt)
        => src.Equals(tgt);
}

public class SearchRegex : SearchType
{
    private string? RegexErrorMessage;
    public override string GetName() => "Regex";
    public override bool SearchMatches(string src, string tgt)
    {
        var bMatched = false;
        try
        {
            bMatched = Regex.IsMatch(src, tgt);
            RegexErrorMessage = null;
        }
        catch (Exception ex)
        {
            RegexErrorMessage = ex.Message;
        }
        return bMatched;
    }
    public override bool PostTableError()
    {
        if (RegexErrorMessage != null)
            ImGui.Text(RegexErrorMessage);
        return RegexErrorMessage != null;
    }
}

public abstract class Searchbar
{
    private static List<SearchType> SearchTypes = [
        new SearchContaining(),
        new SearchWholeWord(),
        new SearchRegex(),
    ];
    
    public SearchType SearchTypeSelected = SearchTypes[0];
    protected ResizableTextInput SearchInput;
    public string? SearchInputStr;
    protected bool bAutoRefresh = true;

    protected Searchbar()
    {
        SearchInput = new ResizableTextInput($"##Search{GetHashCode()}");
        SearchInputStr = null;
    }
    
    public unsafe void DrawPanel()
    {
        var areaAvailable = new ImVec2.__Internal();
        ImGui.__Internal.GetContentRegionAvail((nint)(&areaAvailable));
        ImGui.__Internal.SetNextItemWidth(areaAvailable.x * 0.5f);
        var bStartSearch = SearchInput.Draw();
        ImGui.SameLine(0, 10);
        ImGui.__Internal.SetNextItemWidth(areaAvailable.x * 0.2f);
        if (ImGui.BeginCombo("Search type", SearchTypeSelected.GetName(), 0))
        {
            var searchTypeSelectedIndex = SearchTypes.IndexOf(SearchTypeSelected);
            for (var i = 0; i < SearchTypes.Count; i++)
            {
                if (ImGui.SelectableBool(SearchTypes[i].GetName(), searchTypeSelectedIndex == i, 0, ImGui.ImVec2ImVec2Nil()))
                    SearchTypeSelected = SearchTypes[i];
                if (searchTypeSelectedIndex == i)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine(0, 10);
        ImGui.Checkbox("Auto refresh", ref bAutoRefresh);
        if (bStartSearch)
        {
            SearchInputStr = Marshal.PtrToStringAnsi((nint)SearchInput.GetBuffer());
            if (SearchInputStr is { Length: > 0 })
                OnSearchResult();
            else OnSearchClear();
        }
    }

    public virtual void ShowTableResults(int resultCount)
    {
        if (!SearchTypeSelected.PostTableError())
            ImGui.Text($"Showing {resultCount} results");
        else bAutoRefresh = false;
    }

    public abstract void OnSearchResult();
    public abstract void OnSearchClear();
    public bool GetAutoRefresh() => bAutoRefresh;

    public bool SearchMatches(string Value)
        => SearchInputStr == null || SearchTypeSelected.SearchMatches(Value, SearchInputStr);
    
}

public abstract class ContextualSearchbar<TContextType> : Searchbar where TContextType : class
{
    protected TContextType Context;
    protected abstract string GetTag();

    protected ContextualSearchbar(TContextType context) : base() 
    {
        SearchInput = new ResizableTextInput($"##{GetTag()}{GetHashCode()}");
        Context = context;
    }
}

public class ObjectSearch(WeakReference<App> context) : ContextualSearchbar<WeakReference<App>>(context)
{
    protected override string GetTag() => "UObjectSearch";

    public override void OnSearchResult()
    {
        if (!Context.TryGetTarget(out var App)) return;
        App.ListOfObjects.Clear();
        var GUObjectArray = App.Context.UnrealObjects.GUObjectArray;
        for (var i = 0; i < GUObjectArray.NumElements; i++)
        {
            var CurrentObject = GUObjectArray.IndexToObject(i);
            if (CurrentObject != null && SearchMatches(CurrentObject.NamePrivate.ToString()))
                App.ListOfObjects[CurrentObject.Ptr] = CurrentObject;
        }
    }

    public override void OnSearchClear() => OnSearchResult();
}