﻿using FakeItEasy;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FastEndpoints.UnitTests;

public class EventQueueTests
{
    [Fact]
    public async Task subscriber_storage_queue_and_dequeue()
    {
        var sut = new InMemoryEventSubscriberStorage();

        var record1 = new InMemoryEventStorageRecord
        {
            Event = "test1",
            EventType = "System.String",
            ExpireOn = DateTime.UtcNow.AddMinutes(1),
            SubscriberID = "sub1"
        };

        var record2 = new InMemoryEventStorageRecord
        {
            Event = "test2",
            EventType = "System.String",
            ExpireOn = DateTime.UtcNow.AddMinutes(1),
            SubscriberID = "sub1"
        };

        var record3 = new InMemoryEventStorageRecord
        {
            Event = "test3",
            EventType = "System.String",
            ExpireOn = DateTime.UtcNow.AddMinutes(1),
            SubscriberID = "sub2"
        };

        await sut.StoreEventAsync(record1, default);
        await sut.StoreEventAsync(record2, default);

        var r1x = await sut.GetNextEventAsync(record1.SubscriberID, default);
        r1x!.Event.Should().Be(record1.Event);

        var r2x = await sut.GetNextEventAsync(record1.SubscriberID, default);
        r1x!.Event.Should().Be(record1.Event);

        await sut.MarkEventAsCompleteAsync(r2x!, default);

        await sut.StoreEventAsync(record3, default);

        var r3 = await sut.GetNextEventAsync(record3.SubscriberID, default);
        r3!.Event.Should().Be(record3.Event);

        var r3x = await sut.GetNextEventAsync(record2.SubscriberID, default);
        r3x!.Event.Should().Be(record2.Event);

        await sut.MarkEventAsCompleteAsync(r3x!, default);

        var r4x = await sut.GetNextEventAsync(record2.SubscriberID, default);
        r4x!.Should().BeNull();
    }

    [Fact]
    public async Task subscriber_storage_queue_overflow()
    {
        var sut = new InMemoryEventSubscriberStorage();

        for (var i = 0; i <= 1000; i++)
        {
            var r = new InMemoryEventStorageRecord
            {
                Event = i,
                EventType = "System.String",
                ExpireOn = DateTime.UtcNow.AddMinutes(1),
                SubscriberID = "sub1"
            };

            if (i < 1000)
            {
                await sut.StoreEventAsync(r, default);
            }
            else
            {
                var func = async () => await sut.StoreEventAsync(r, default);
                await func.Should().ThrowAsync<OverflowException>();
            }
        }
    }

    [Fact]
    public async Task subscriber_hub_success_path()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        EventPublisherStorage.Initialize<InMemoryEventStorageRecord, InMemoryEventPublisherStorage>(provider);

        var hub = new EventHub<TestEvent>();
        var logger = A.Fake<ILogger>();
        EventHub<TestEvent>.Logger = logger;

        var writer = new TestServerStreamWriter<TestEvent>();

        var ctx = A.Fake<ServerCallContext>();
        A.CallTo(ctx).WithReturnType<CancellationToken>().Returns(default);

        _ = hub.OnClientConnected(hub, "sub1", writer, ctx);
        _ = hub.OnClientConnected(hub, "sub2", writer, ctx);

        var e1 = new TestEvent { EventID = 0 };
        EventHub<TestEvent>.AddToSubscriberQueues(e1, default);

        await Task.Delay(500);

        writer.Responses[0].EventID.Should().Be(0);
        writer.Responses[1].EventID.Should().Be(0);
    }

    private class TestEvent : IEvent
    {
        public int EventID { get; set; }
    }

    private class TestServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public WriteOptions? WriteOptions { get; set; }
        public List<T> Responses { get; } = new List<T>();

        public async Task WriteAsync(T message)
            => Responses.Add(message);
    }
}