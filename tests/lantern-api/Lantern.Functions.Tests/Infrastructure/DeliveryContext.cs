using System.Text;
using Microsoft.Azure.Functions.Worker;
using Moq;

namespace Lantern.Functions.Tests.Infrastructure;

internal static class DeliveryContext
{
    public static FunctionContext AfterCycles(int cycles)
    {
        var data = new Dictionary<string, object> { ["BasicProperties"] = Properties(cycles) };
        var binding = new Mock<BindingContext>();
        binding.SetupGet(b => b.BindingData).Returns(data);
        var context = new Mock<FunctionContext>();
        context.SetupGet(c => c.BindingContext).Returns(binding.Object);

        return context.Object;
    }

    private static string Properties(int cycles) =>
        cycles == 0
            ? "{\"Headers\":null}"
            : "{\"Headers\":{\"x-ms-rabbitmq-requeuecount\":4,\"x-death\":["
                + $"{{\"queue\":\"{Encode("work-delay")}\",\"reason\":\"{Encode("expired")}\",\"count\":{cycles}}},"
                + $"{{\"queue\":\"{Encode("work")}\",\"reason\":\"{Encode("rejected")}\",\"count\":{cycles}}}]}}}}";

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
