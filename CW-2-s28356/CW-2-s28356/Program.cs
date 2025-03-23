using System;
using System.Collections.Generic;
using System.Linq;

namespace ContainerShipExample
{
    public class OverfillException : Exception
    {
        public OverfillException(string message) : base(message)
        {
        }
    }

    public interface IHazardNotifier
    {
        void NotifyHazard(string message);
    }

    public abstract class Container
    {
        public string SerialNumber { get; }
        public double ContainerWeight { get; }
        public double MaxLoadWeight { get; }
        public double CurrentLoad { get; protected set; }
        public double Height { get; }
        public double Depth { get; }

        protected Container(
            string serialNumber,
            double containerWeight,
            double maxLoadWeight,
            double height,
            double depth)
        {
            SerialNumber = serialNumber;
            ContainerWeight = containerWeight;
            MaxLoadWeight = maxLoadWeight;
            Height = height;
            Depth = depth;
            CurrentLoad = 0.0;
        }

        public virtual void Load(double mass)
        {
            if (CurrentLoad + mass > MaxLoadWeight)
            {
                throw new OverfillException($"Przekroczono maksymalną ładowność kontenera {SerialNumber}!");
            }
            CurrentLoad += mass;
        }

        public virtual void Unload()
        {
            CurrentLoad = 0.0;
        }

        public virtual string GetInfo()
        {
            return $"[{SerialNumber}] " +
                   $"Waga kontenera: {ContainerWeight} kg, " +
                   $"Maks. ładowność: {MaxLoadWeight} kg, " +
                   $"Aktualnie załadowane: {CurrentLoad} kg, " +
                   $"Wys.: {Height} cm, Gł.: {Depth} cm";
        }
    }

    public class LiquidContainer : Container, IHazardNotifier
    {
        private readonly bool _dangerousCargo;
        private bool _hazardFlag;

        public LiquidContainer(
            string serialNumber,
            double containerWeight,
            double maxLoadWeight,
            double height,
            double depth,
            bool dangerousCargo)
            : base(serialNumber, containerWeight, maxLoadWeight, height, depth)
        {
            _dangerousCargo = dangerousCargo;
            _hazardFlag = false;
        }

        public override void Load(double mass)
        {
            var limit = _dangerousCargo ? MaxLoadWeight * 0.5 : MaxLoadWeight * 0.9;
            if (CurrentLoad + mass > limit)
            {
                NotifyHazard($"Próba przekroczenia limitu bezpieczeństwa kontenera płynnego: {SerialNumber}");
                throw new OverfillException($"Niebezpieczne przeładowanie kontenera płynnego: {SerialNumber}");
            }
            base.Load(mass);
        }

        public void NotifyHazard(string message)
        {
            _hazardFlag = true;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("HAZARD! " + message);
            Console.ResetColor();
        }

        public override string GetInfo()
        {
            var cargoType = _dangerousCargo ? "[Ładunek NIEBEZPIECZNY]" : "[Ładunek zwykły]";
            return base.GetInfo() + " " + cargoType;
        }
    }

    public class GasContainer : Container, IHazardNotifier
    {
        public double Pressure { get; private set; }
        private bool _hazardFlag;

        public GasContainer(
            string serialNumber,
            double containerWeight,
            double maxLoadWeight,
            double height,
            double depth,
            double pressure)
            : base(serialNumber, containerWeight, maxLoadWeight, height, depth)
        {
            Pressure = pressure;
            _hazardFlag = false;
        }

        public override void Unload()
        {
            var remain = CurrentLoad * 0.05;
            base.Unload(); 
            try
            {
                base.Load(remain);
            }
            catch (OverfillException e)
            {
                Console.Error.WriteLine(e.Message);
            }
        }

        public override void Load(double mass)
        {
            if (CurrentLoad + mass > MaxLoadWeight)
            {
                NotifyHazard($"Przekroczono dopuszczalną ładowność w kontenerze gazowym: {SerialNumber}");
                throw new OverfillException($"Próba przeładowania kontenera gazowego: {SerialNumber}");
            }
            base.Load(mass);
        }

        public void NotifyHazard(string message)
        {
            _hazardFlag = true;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("HAZARD! " + message);
            Console.ResetColor();
        }

        public void SetPressure(double newPressure)
        {
            Pressure = newPressure;
        }

        public override string GetInfo()
        {
            return base.GetInfo() + $" [Ciśnienie: {Pressure} atm]";
        }
    }

    public class ReeferContainer : Container
    {
        public string ProductType { get; }
        public double RequiredTemp { get; }
        public double CurrentContainerTemp { get; private set; }

        public ReeferContainer(
            string serialNumber,
            double containerWeight,
            double maxLoadWeight,
            double height,
            double depth,
            string productType,
            double requiredTemp,
            double currentTemp)
            : base(serialNumber, containerWeight, maxLoadWeight, height, depth)
        {
            ProductType = productType;
            RequiredTemp = requiredTemp;
            CurrentContainerTemp = currentTemp;
        }

        public void SetCurrentContainerTemp(double temp)
        {
            if (temp < RequiredTemp)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine(
                    $"Ostrzeżenie: próba ustawienia temp {temp}°C poniżej wymaganej ({RequiredTemp}°C). Ignoruję.");
                Console.ResetColor();
            }
            else
            {
                CurrentContainerTemp = temp;
            }
        }

        public override string GetInfo()
        {
            return base.GetInfo() +
                   $" [Produkt: {ProductType}, Wymagana temp: {RequiredTemp}°C, " +
                   $"Aktualna temp: {CurrentContainerTemp}°C]";
        }
    }

    public class Ship
    {
        public string Name { get; }
        public double Speed { get; }        
        public int MaxContainerNum { get; }
        public double MaxWeight { get; }    
        private List<Container> _containersOnBoard;

        public Ship(string name, double speed, int maxContainerNum, double maxWeight)
        {
            Name = name;
            Speed = speed;
            MaxContainerNum = maxContainerNum;
            MaxWeight = maxWeight; // w tonach
            _containersOnBoard = new List<Container>();
        }

        public void AddContainer(Container container)
        {
            if (_containersOnBoard.Count >= MaxContainerNum)
            {
                throw new InvalidOperationException(
                    $"Nie można dodać kontenera – przekroczono limit liczby kontenerów na statku {Name}");
            }

            var totalWeightKg = GetTotalWeightKg() + container.ContainerWeight + container.CurrentLoad;
            var totalWeightTons = totalWeightKg / 1000.0;
            if (totalWeightTons > MaxWeight)
            {
                throw new InvalidOperationException(
                    $"Nie można dodać kontenera – przekroczono maksymalną masę (w tonach) dla statku {Name}");
            }

            _containersOnBoard.Add(container);
        }

        public void RemoveContainer(string serialNumber)
        {
            var toRemove = _containersOnBoard.FirstOrDefault(c => c.SerialNumber == serialNumber);
            if (toRemove == null)
            {
                throw new InvalidOperationException(
                    $"Kontener {serialNumber} nie znajduje się na statku {Name}");
            }
            _containersOnBoard.Remove(toRemove);
        }

        private double GetTotalWeightKg()
        {
            double sum = 0.0;
            foreach (var c in _containersOnBoard)
            {
                sum += c.ContainerWeight + c.CurrentLoad;
            }
            return sum;
        }

        public void PrintShipInfo()
        {
            Console.WriteLine($"=== Statek: {Name} ===");
            Console.WriteLine($"Szybkość (węzły): {Speed}");
            Console.WriteLine($"Maks. liczba kontenerów: {MaxContainerNum}");
            Console.WriteLine($"Maks. waga (tony): {MaxWeight}");
            Console.WriteLine($"Aktualnie na pokładzie: {_containersOnBoard.Count} kontenerów.");
            foreach (var c in _containersOnBoard)
            {
                Console.WriteLine("   -> " + c.GetInfo());
            }
            var totalWeightTons = GetTotalWeightKg() / 1000.0;
            Console.WriteLine($"Łączna masa kontenerów (z ładunkiem): {totalWeightTons:F2} t\n");
        }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                var ship1 = new Ship("Statek 1", 10.0, 3, 40.0);

                var liquidContainerSafe = new LiquidContainer("KON-C-1", 1000, 5000, 200, 100, false);
                var liquidContainerDanger = new LiquidContainer("KON-C-2", 1200, 6000, 220, 120, true);
                var gasContainer = new GasContainer("KON-C-3", 800, 4000, 180, 90, 2.0);
                var reeferContainer = new ReeferContainer("KON-C-4", 1500, 3000, 200, 120,
                    "Bananas", 13.3, 15.0);

                liquidContainerSafe.Load(4000);
                liquidContainerDanger.Load(2500);

                ship1.AddContainer(liquidContainerSafe);
                ship1.AddContainer(liquidContainerDanger);
                ship1.AddContainer(gasContainer);
                try
                {
                    gasContainer.Load(4500); 
                }
                catch (OverfillException e)
                {
                    Console.Error.WriteLine("Błąd przy ładowaniu gazu: " + e.Message);
                }

                ship1.RemoveContainer("KON-C-2");
                ship1.AddContainer(reeferContainer);

                reeferContainer.SetCurrentContainerTemp(10.0); 

                ship1.PrintShipInfo();

                gasContainer.Unload();
                Console.WriteLine("Po rozładowaniu kontenera gazowego:");
                ship1.PrintShipInfo();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Błąd: {e.Message}");
            }

            Console.WriteLine("Koniec demonstracji. Naciśnij dowolny klawisz, aby zakończyć.");
            Console.ReadKey();
        }
    }
}
