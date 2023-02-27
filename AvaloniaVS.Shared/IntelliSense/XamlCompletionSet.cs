#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace AvaloniaVS.IntelliSense;

internal class XamlCompletionSet : CompletionSet2
{
    private readonly FilteredObservableCollection<Completion> currentCompletions;
    private readonly BulkObservableCollection<Completion> _completions = new BulkObservableCollection<Completion>();
    public readonly List<string> _activeFilters = new List<string>();
    private string _typed = string.Empty;
    private static readonly List<Span> _defaultEmptyList = new();
    private Predicate<Completion>? _searchByAutomationText;
    private Predicate<Completion>? _searchByDisplayText;

    private Predicate<Completion> SearchByAutomationText => _searchByAutomationText
        ??= new(DoesCompletionMatchAutomationText);

    private Predicate<Completion> SearchByDisplayText => _searchByDisplayText
        ??= new(DoesCompletionMatchDisplayText);

    private XamlCompletionSet(ITrackingSpan applicableTo,
        IEnumerable<Completion> completions,
        IReadOnlyList<IIntellisenseFilter> filters) :
        base("Avalonia", "Avalona XAML", applicableTo, completions, null, filters)
    {
        _completions.AddRange(completions);
        currentCompletions = new FilteredObservableCollection<Completion>(_completions);
    }

    public override IList<Completion> Completions => currentCompletions;

    public static CompletionSet Create(ITrackingSpan applicableTo,
        IEnumerable<Completion> completions,
        IReadOnlyList<IIntellisenseFilter> filters)
    {
        return new XamlCompletionSet(applicableTo, completions, filters);
    }

    public override void Filter()
    {
        // This is handled in SelectBestMatch
    }

    public override void SelectBestMatch()
    {
        _typed = ApplicableTo.GetText(ApplicableTo.TextBuffer.CurrentSnapshot);
        CustomFilter();
        base.SelectBestMatch();
    }

    private void CustomFilter()
    {
        IReadOnlyList<IIntellisenseFilter> currentActiveFilters = Filters;

        if (currentActiveFilters != null && currentActiveFilters.Count > 0)
        {
            IEnumerable<string> activeFilters = currentActiveFilters.Where(f => f.IsChecked).Select(f => f.AutomationText);

            if (!activeFilters.Any())
                activeFilters = currentActiveFilters.Select(f => f.AutomationText);

            _activeFilters.Clear();
            _activeFilters.AddRange(activeFilters);

            currentCompletions.Filter(SearchByAutomationText);
        }
        else
        {
            currentCompletions.Filter(SearchByDisplayText);
        }
    }

    private bool DoesCompletionMatchDisplayText(Completion completion) => 
        _typed.Length == 0 
        || completion.DisplayText.IndexOf(_typed, StringComparison.OrdinalIgnoreCase) > -1;

    private bool DoesCompletionMatchAutomationText(Completion completion) => 
        _activeFilters.Exists(x => x.Equals(completion.IconAutomationText, StringComparison.OrdinalIgnoreCase)) &&
              (_typed.Length == 0 || completion.DisplayText.IndexOf(_typed, StringComparison.OrdinalIgnoreCase) > -1);

}
