using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

// Enums for Elevator Directions and Status
public enum Direction
{
    Up,
    Down,
    None
}

public enum ElevatorStatus
{
    Running,
    Stopped
}

// Observer Pattern: IObserver Interface
public interface IObserver
{
    void Update(int floorNumber, Direction direction);
}

// Floor Interface
public interface IFloor
{
    int FloorNumber { get; }
    void AddElevator(IElevator elevator);
    IEnumerable<IElevator> GetElevators();
    void PressExternalButton(Direction direction);
    void RegisterObserver(IObserver observer);
}

// Elevator Interface
public interface IElevator
{
    int ElevatorId { get; }
    int CurrentFloor { get; }
    Direction CurrentDirection { get; }
    void AddInternalRequest(int floorNumber);
    void AddExternalRequest(int floorNumber);
    void MoveToFloor(int floorNumber);
}

// Command Pattern: ICommand Interface
public interface ICommand
{
    void Execute();
}

// Request Handler Strategy Interface (Strategy Pattern)
public interface IRequestHandlerStrategy
{
    IElevator? SelectElevator(IEnumerable<IElevator> elevators, IFloor currentFloor, Direction direction);
}

// Floor Class Implementing Observer Pattern
public class Floor : IFloor
{
    public int FloorNumber { get; }
    private readonly List<IElevator> _elevators = new List<IElevator>();
    private readonly List<IObserver> _observers = new List<IObserver>();

    public Floor(int floorNumber)
    {
        FloorNumber = floorNumber;
    }

    public void AddElevator(IElevator elevator)
    {
        if (_elevators.All(e => e.ElevatorId != elevator.ElevatorId))
        {
            _elevators.Add(elevator);
        }
    }

    public IEnumerable<IElevator> GetElevators()
    {
        return _elevators;
    }

    public void RegisterObserver(IObserver observer)
    {
        if (!_observers.Contains(observer))
        {
            _observers.Add(observer);
        }
    }

    public void PressExternalButton(Direction direction)
    {
        NotifyObservers(direction);
    }

    private void NotifyObservers(Direction direction)
    {
        foreach (var observer in _observers)
        {
            observer.Update(FloorNumber, direction);
        }
    }
}

// Elevator Class Implementing Request Handling
public class Elevator : IElevator
{
    public int ElevatorId { get; }
    public int CurrentFloor { get; private set; }
    public Direction CurrentDirection { get; private set; }
    public ElevatorStatus Status { get; private set; }

    private readonly PriorityQueue<int, int> _upRequests;
    private readonly PriorityQueue<int, int> _downRequests;
    private bool _doorOpen;

    public Elevator(int elevatorId)
    {
        ElevatorId = elevatorId;
        CurrentDirection = Direction.None;
        Status = ElevatorStatus.Stopped;
        _upRequests = new PriorityQueue<int, int>();
        _downRequests = new PriorityQueue<int, int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
    }

    public void AddInternalRequest(int floorNumber)
    {
        if (floorNumber > CurrentFloor)
        {
            _upRequests.Enqueue(floorNumber, floorNumber);
        }
        else if (floorNumber < CurrentFloor)
        {
            _downRequests.Enqueue(floorNumber, floorNumber);
        }
        ProcessNextRequest();
    }

    public void AddExternalRequest(int floorNumber)
    {
        if (floorNumber > CurrentFloor)
        {
            _upRequests.Enqueue(floorNumber, floorNumber);
        }
        else
        {
            _downRequests.Enqueue(floorNumber, floorNumber);
        }
        ProcessNextRequest();
    }

    public void MoveToFloor(int floorNumber)
    {
        if (!_doorOpen)
        {
            CurrentDirection = floorNumber > CurrentFloor ? Direction.Up : Direction.Down;
            Console.WriteLine($"Elevator {ElevatorId} moving to floor {floorNumber}.");
            Thread.Sleep(1000); // Simulate movement
            CurrentFloor = floorNumber;
            Console.WriteLine($"Elevator {ElevatorId} arrived at floor {floorNumber}.");
            OpenDoor();
        }
        else
        {
            Console.WriteLine("Cannot move elevator with doors open.");
        }
    }

    private void OpenDoor()
    {
        if (!_doorOpen)
        {
            _doorOpen = true;
            Console.WriteLine($"Elevator {ElevatorId} door opened at floor {CurrentFloor}.");
            Thread.Sleep(500);
            CloseDoor();
        }
    }

    private void CloseDoor()
    {
        if (_doorOpen)
        {
            _doorOpen = false;
            Console.WriteLine($"Elevator {ElevatorId} door closed at floor {CurrentFloor}.");
            Thread.Sleep(200);
            ProcessNextRequest();
        }
    }

    private void ProcessNextRequest()
    {
        if (CurrentDirection == Direction.Up && _upRequests.Count > 0)
        {
            MoveToFloor(_upRequests.Dequeue());
        }
        else if (CurrentDirection == Direction.Down && _downRequests.Count > 0)
        {
            MoveToFloor(_downRequests.Dequeue());
        }
        else
        {
            StopElevator();
        }
    }

    private void StopElevator()
    {
        CurrentDirection = Direction.None;
        Status = ElevatorStatus.Stopped;
    }
}

// Command Pattern: ExternalRequestCommand Class
public class ExternalRequestCommand : ICommand
{
    private readonly IRequestHandlerStrategy _strategy;
    private readonly IFloor _currentFloor;
    private readonly Direction _direction;

    public ExternalRequestCommand(IRequestHandlerStrategy strategy, IFloor currentFloor, Direction direction)
    {
        _strategy = strategy;
        _currentFloor = currentFloor;
        _direction = direction;
    }

    public void Execute()
    {
        var elevators = _currentFloor.GetElevators();
        var selectedElevator = _strategy.SelectElevator(elevators, _currentFloor, _direction);
        if (selectedElevator != null)
        {
            selectedElevator.AddExternalRequest(_currentFloor.FloorNumber);
        }
        else
        {
            Console.WriteLine("No available elevators. Please wait.");
        }
    }
}

// Observer Pattern: ExternalDisplay Class
public class ExternalDisplay : IObserver
{
    private readonly ICommand _command;

    public ExternalDisplay(ICommand command)
    {
        _command = command;
    }

    public void Update(int floorNumber, Direction direction)
    {
        Console.WriteLine($"External display on floor {floorNumber} updating for {direction} request.");
        _command.Execute();
    }
}

public class NearestElevatorStrategy : IRequestHandlerStrategy
{
    public IElevator? SelectElevator(IEnumerable<IElevator> elevators, IFloor currentFloor, Direction direction)
    {
        // Filter eligible elevators based on the requested direction
        var eligibleElevators = elevators
            .Where(elevator => IsElevatorEligible(elevator, currentFloor, direction))
            .OrderBy(elevator => CalculateDistance(elevator, currentFloor))
            .ToList();

        // If no elevators are eligible in the requested direction, fallback to idle elevators
        if (!eligibleElevators.Any())
        {
            eligibleElevators = elevators
                .Where(elevator => elevator.CurrentDirection == Direction.None)
                .OrderBy(elevator => CalculateDistance(elevator, currentFloor))
                .ToList();
        }

        // Return the closest eligible elevator, or null if none are available
        return eligibleElevators.FirstOrDefault();
    }

    private bool IsElevatorEligible(IElevator elevator, IFloor currentFloor, Direction direction)
    {
        // Determine if the elevator can service the request based on its direction
        switch (direction)
        {
            case Direction.Up:
                return elevator.CurrentFloor <= currentFloor.FloorNumber &&
                       (elevator.CurrentDirection == Direction.Up || elevator.CurrentDirection == Direction.None);

            case Direction.Down:
                return elevator.CurrentFloor >= currentFloor.FloorNumber &&
                       (elevator.CurrentDirection == Direction.Down || elevator.CurrentDirection == Direction.None);

            default:
                return false;
        }
    }

    private static int CalculateDistance(IElevator elevator, IFloor currentFloor)
    {
        // Calculate the absolute distance between the elevator and the requested floor
        return Math.Abs(elevator.CurrentFloor - currentFloor.FloorNumber);
    }
}



// StrategyManager Class to Manage Strategies
public class StrategyManager
{
    private readonly Dictionary<string, IRequestHandlerStrategy> _strategies;

    public StrategyManager()
    {
        _strategies = new Dictionary<string, IRequestHandlerStrategy>();
    }

    public void RegisterStrategy(string name, IRequestHandlerStrategy strategy)
    {
        if (!_strategies.ContainsKey(name))
        {
            _strategies[name] = strategy;
        }
    }

    public IRequestHandlerStrategy? GetStrategy(string name)
    {
        return _strategies.TryGetValue(name, out var strategy) ? strategy : null;
    }
}

// Factory Pattern: Factory Class
public static class Factory
{
    public static IFloor CreateFloor(int floorNumber)
    {
        return new Floor(floorNumber);
    }

    public static IElevator CreateElevator(int elevatorId)
    {
        return new Elevator(elevatorId);
    }

    public static IObserver CreateExternalDisplay(ICommand command)
    {
        return new ExternalDisplay(command);
    }

    public static ICommand CreateExternalRequestCommand(IRequestHandlerStrategy strategy, IFloor floor, Direction direction)
    {
        return new ExternalRequestCommand(strategy, floor, direction);
    }

    public static StrategyManager CreateStrategyManager()
    {
        var strategyManager = new StrategyManager();
        strategyManager.RegisterStrategy("NearestElevator", new NearestElevatorStrategy());
        // Additional strategies can be registered here.
        return strategyManager;
    }
}

// Program Entry Point
public class Program
{
    public static void Main(string[] args)
    {
        var floors = Enumerable.Range(0, 6).Select(Factory.CreateFloor).ToArray();
        var elevators = new[] { Factory.CreateElevator(1), Factory.CreateElevator(2) };
        var strategyManager = Factory.CreateStrategyManager();

        var selectedStrategy = strategyManager.GetStrategy("NearestElevator");
        if (selectedStrategy == null)
        {
            Console.WriteLine("Selected strategy is not available.");
            return;
        }

        foreach (var floor in floors)
        {
            foreach (var elevator in elevators)
            {
                floor.AddElevator(elevator);
            }

            var command = Factory.CreateExternalRequestCommand(selectedStrategy, floor, Direction.Up);
            var externalDisplay = Factory.CreateExternalDisplay(command);
            floor.RegisterObserver(externalDisplay);
        }

        // Simulate pressing external buttons
        floors[1].PressExternalButton(Direction.Up);
        floors[5].PressExternalButton(Direction.Down);

        // Simulate internal elevator requests
        elevators[0].AddInternalRequest(3);
        elevators[1].AddInternalRequest(4);
    }
}
