using System;
using System.Collections.Generic;
using System.Linq;
using UnitsNet;

public class UnitsDto
{
    public string Type { get; set; }
    public string[] Units { get; set; }
}

public class Program
{
    public static void Main()
    {
        var allUnits = new List<UnitsDto>();
        QuantityType[] quantityTypes = Enum.GetValues(typeof(QuantityType)).Cast<QuantityType>().ToArray();

        foreach (QuantityType unitValue in quantityTypes)
        {
            if (unitValue != QuantityType.Undefined)
            {
                QuantityInfo qtyInfo = Quantity.GetInfo(unitValue);
                allUnits.Add(
                    new UnitsDto
                    {
                        Type = qtyInfo.Name,
                        Units = qtyInfo.UnitInfos.Select(u => u.Name).ToArray()
                    });
            }
        }

        foreach (var quantity in allUnits.OrderBy(q => q.Type))
        {
            Console.WriteLine($"### {quantity.Type}\n");
            foreach (var unit in quantity.Units.OrderBy(u => u))
            {
                Console.WriteLine($"- {unit}");
            }
            Console.WriteLine();
        }
    }
}
