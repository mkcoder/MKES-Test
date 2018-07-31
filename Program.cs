using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ConsoleTables;
using MKES;
using MKES.Attributes;
using MKES.EventStore;
using MKES.Interfaces;
using MKES.Model;
using MKES.EventBus;
using Newtonsoft.Json;
using RabbitMQ.Client.Events;

namespace ConsoleApp4
{
    class Program
    {
        static void Main(string[] args)
        {
            InventoryAggregate inventory = new InventoryAggregate();
            inventory.SendCommand(new AddItemToInventoryCommand() {Item = "Sword", Quantity = 1});
            inventory.SendCommand(new AddItemToInventoryCommand() {Item = "Apple", Quantity = 3});
            inventory.SendCommand(new AddItemToInventoryCommand() {Item = "Armour", Quantity = 2});
            inventory.SendCommand(new AddItemToInventoryCommand() {Item = "Banana", Quantity = 1});
            inventory.SendCommand(new AddItemToInventoryCommand() {Item = "Helmet", Quantity = 4});
            inventory.SendCommand(new AddItemToInventoryCommand() {Item = "Sword", Quantity = 1});
            inventory.SendCommand(new RemoveItemFromInventory() { Id = 3});
            inventory.SendCommand(new AddItemToInventoryCommand() { Item = "Sword", Quantity = 12 });
            inventory.SendCommand(new UpdateItemInInventory() { Id = 1, Item = "Sword", Quantity = 12 });
        }
    }

    public class InventoryAggregate : AggregateRoot
    {
        private readonly IEventBus _eventBus;
        private int _highestId = 0;
        private List<InventoryItem> _inventory = new List<InventoryItem>();

        public InventoryAggregate() : this(new DefaultEventStoreRepository(new EventStoreImpl(), new EventBusImpl()))
        {
        }

        private InventoryAggregate(IEventStoreRepository eventStoreRepository) : base(eventStoreRepository)
        {
            _eventBus = new EventBusImpl();
            Register<ItemAddedToInventory>(Apply);
            Register<ItemRemovedFromInventory>(Apply);
            Register<ItemUpdatedInInventory>(Apply);
            _eventBus.RegisterAsync(ListenerInfo.FromCommand(typeof(AddItemToInventoryCommand)), AddItemToInventoryCommand);
            _eventBus.RegisterAsync(ListenerInfo.FromCommand(typeof(RemoveItemFromInventory)), RemoveItemFromInventoryCommand);
            _eventBus.RegisterAsync(ListenerInfo.FromCommand(typeof(UpdateItemInInventory)), UpdateItemInInventoryCommand);
        }

        public void SendCommand(Command command)
        {
            _eventBus.Send(command);
        }

        private async Task RemoveItemFromInventoryCommand(object sender, BasicDeliverEventArgs @event)
        {
            var body = Encoding.UTF8.GetString(@event.Body);
            var model = JsonConvert.DeserializeObject<RemoveItemFromInventory>(body);
            Console.WriteLine($"Remove this item {model}");
            await Task.Run(() =>
            {
                ApplyChanges(new ItemRemovedFromInventory() {Id = model.Id});
            });
        }

        private async Task AddItemToInventoryCommand(object sender, BasicDeliverEventArgs @event)
        {
            var body = Encoding.UTF8.GetString(@event.Body);
            var model = JsonConvert.DeserializeObject<AddItemToInventoryCommand>(body);
            await Task.Run(() =>
            {
                ApplyChanges(new ItemAddedToInventory()
                {
                    AggregateId = model.AggregateId,
                    Id = ++_highestId,
                    Item = model.Item,
                    Quantity = model.Quantity,
                    Version = model.Version
                });
            });
        }

        private async Task UpdateItemInInventoryCommand(object sender, BasicDeliverEventArgs @event)
        {
            var body = Encoding.UTF8.GetString(@event.Body);
            var model = JsonConvert.DeserializeObject<UpdateItemInInventory>(body);
            await Task.Run(() =>
            {
                ApplyChanges(new ItemUpdatedInInventory()
                {
                    Id = model.Id,
                    Quantity = model.Quantity,
                    Item = model.Item
                });
            });
        }

        private void Apply(ItemRemovedFromInventory obj)
        {
            _inventory.RemoveAll(a => a.Id == obj.Id);
            Print();
        }

        private void Apply(ItemAddedToInventory obj)
        {
            _inventory.Add(new InventoryItem() {
                AggregateId = obj.AggregateId,
                Id = obj.Id,
                Item = obj.Item,
                Quantity = obj.Quantity,
            });
            Print();

        }

        private void Apply(ItemUpdatedInInventory obj)
        {
            var item = _inventory.Find(i => i.Id == obj.Id);
            item.Item = obj.Item;
            item.Quantity = obj.Quantity;
            Print();
        }

        public void Print()
        {
           Console.Clear();
           ConsolePrinter.PrintInventory(_inventory);
        }
    }

    public class InventoryItem
    {
        public Guid AggregateId { get; set; }
        public int Id { get; set; }
        public string Item { get; set; }
        public int Quantity { get; set; }
    }

    public class ItemAddedToInventory : Event
    {
        public int Id { get; set; }
        public string Item { get; set; }
        public int Quantity { get; set; }
    }

    public class ItemRemovedFromInventory : Event
    {
        public int Id { get; set; }
    }

    public class ItemUpdatedInInventory : Event
    {
        public int Id { get; set; }
        public string Item { get; set; }
        public int Quantity { get; set; }
    }

    [CommandInfo(name: "InventoryAggregate", routingKey: "AddItemToInventoryCommand")]
    public class AddItemToInventoryCommand : Command
    {
        public int Id { get; set; }
        public string Item { get; set; }
        public int Quantity { get; set; }
    }

    [CommandInfo(name: "InventoryAggregate", routingKey: "RemoveItemFromInventoryCommand")]
    public class RemoveItemFromInventory : Command
    {
        public int Id { get; set; }
    }

    [CommandInfo(name: "InventoryAggregate", routingKey: "UpdateItemInInventoryCommand")]
    public class UpdateItemInInventory : Command
    {
        public int Id { get; set; }
        public string Item { get; set; }
        public int Quantity { get; set; }
    }

    public class ConsolePrinter
    {
        public static void PrintInventory(List<InventoryItem> items)
        {
            ConsoleTable table = new ConsoleTable("Id", "Item Name", "Quantity");
            foreach (var tuple in items)
            {
                table.AddRow(tuple.Id, tuple.Item, tuple.Quantity);
            }
            table.Write();
            Console.WriteLine();
        }
    }
}