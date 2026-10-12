namespace CafeMenuConverter.Core.Model;

/// <summary>Одно блюдо меню. Все значения — как в PDF; числа хранятся строками.</summary>
public sealed record MenuItem(
    DateOnly Date,
    string MealType,
    string Category,
    string Name,
    IReadOnlyList<string> Composition,
    IReadOnlyList<string> Weights,
    string Unit,
    string Quantity,
    string Price,
    string Calories,
    string Protein,
    string Fats,
    string Carbohydrates,
    int Page);

public sealed record Office(string Name, string Id)
{
    public static readonly Office Space = new("Space", "7f98361b-7b8d-40f0-bdec-0ef6d811afbf");
    public static readonly Office Vodny = new("Водный", "22ee2228-41b9-4db0-8fb4-1a0217cf41af");
    public static readonly Office Oko = new("ОКО", "a5d27432-a5af-41c6-bc91-9b7ce3ce09b2");

    public static IReadOnlyList<Office> All { get; } = new[] { Space, Vodny, Oko };

    public override string ToString() => Name;
}
