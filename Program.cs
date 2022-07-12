using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("Run the below samples by uncommenting the lines");

        await BasicAsync();
        // await AdvancedAsync();
        // await TimeoutConsequenceAsync();
        // await FallbackWithTimeoutAsync();
    }

    private static async Task BasicAsync()
    {
        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(2, TimeSpan.FromSeconds(1));
        // 2 errors break it for 1 second 

        for (int i = 0; i < 10; ++i)
        {
            try
            {
                Console.WriteLine($"Execution {i}");
                await circuitBreakerPolicy.ExecuteAsync(async () =>
                {
                    Console.WriteLine($"before throw exception {i}");
                    throw new Exception($"Error {i}");
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Catch ex {ex.Message}");
            }
            await Task.Delay(500);
        }
    }

    public static async Task AdvancedAsync()
    {
        var advancedCircuitBreaker =Policy        
            .Handle<Exception>()        
            .AdvancedCircuitBreakerAsync(0.5, TimeSpan.FromSeconds(2), 
                                3, TimeSpan.FromSeconds(1));

        // open if 50% of requests throw errors during 2 seconds with the condition that minimal amount of requests is 3.   

        for (int i = 0; i < 10; i++)    
        {
            try        
            {
                Console.WriteLine($"Execution {i}");
                await advancedCircuitBreaker.ExecuteAsync(async () =>            
                {
                    Console.WriteLine($"before throw exception {i}");
                    throw new Exception($"Error {i}");            
                });        
            }
            catch (Exception ex)        
            {
                Console.WriteLine($"Catch ex {ex.Message}");        
            }
            await Task.Delay(500);    
        }
    }


    public static async Task TimeoutConsequenceAsync()
    {
        var advancedCircuitBreaker=Policy        
            .Handle<Exception>()        
            .AdvancedCircuitBreakerAsync(1, TimeSpan.FromSeconds(3), 
                                        2, TimeSpan.FromSeconds(1));
            
        var timeoutPolicy = Policy.TimeoutAsync
                                    (TimeSpan.FromMilliseconds(1000), 
                                    TimeoutStrategy.Pessimistic);
        // note: Optimistic cancel operation via cancellation token 
        
        var wrapPolicy=Policy.WrapAsync(advancedCircuitBreaker, timeoutPolicy);
        
        // timeout policy wraps code and circuit policy wraps timeout policy.

        for (int i = 0; i < 10; i++)    
        {
            try        
            {
                Console.WriteLine($"Execution {i}");
                await wrapPolicy.ExecuteAsync(async () =>            
                {
                    Console.WriteLine($"before throw exception {i}");
                    await  Task.Delay(TimeSpan.FromMilliseconds(1000));
                    Console.WriteLine($"after throw exception {i}");            
                });
                Console.WriteLine($"Execution {i} after actual call");        
            }
            catch (Exception ex)        
            {
                Console.WriteLine($"Catch ex {ex.Message}");        
            }
            await Task.Delay(100);    
        }
    }

    public static async Task FallbackWithTimeoutAsync()
    {
        var advancedCircuitBreaker = Policy        
            .Handle<Exception>()        
            .AdvancedCircuitBreakerAsync(0.5, TimeSpan.FromSeconds(2), 
                                    3, TimeSpan.FromSeconds(1));
            
        var timeoutPolicy = Policy.TimeoutAsync
                                    (TimeSpan.FromMilliseconds(1000), 
                                    TimeoutStrategy.Pessimistic);
            
        var fallback=Policy        
            .Handle<BrokenCircuitException>()        
            .Or<TimeoutException>()        
            .Or<AggregateException>()        
            .Or<TimeoutRejectedException>()        
            .FallbackAsync((cancellation) =>        
            {
                Console.WriteLine("Fallback action");
                return Task.CompletedTask;        
            });
        
        var wrapPolicy = Policy.WrapAsync(fallback,
                            advancedCircuitBreaker, timeoutPolicy);
        
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)    
        {
            try        
            {
                tasks.Add(wrapPolicy.ExecuteAsync(async () =>            
                {
                    Console.WriteLine($"before wait {i}");
                    await Task.Delay(TimeSpan.FromMilliseconds(3500));
                    Console.WriteLine($"after wait {i}");            
                }));        
            }
            catch (AggregateException ex)        
            {
                // never come here
                Console.WriteLine($"Catch ex {ex.Message}");       
            }
            
            await Task.Delay(500);    
        }
        try    
        {
            await Task.WhenAll(tasks);    
        }
        
        catch (AggregateException)    
        {
            // here ex contains first error thrown by list of tasks
            var errors = tasks.Where(t => t.Exception != null).Select
                                                (t => t.Exception);
            foreach (var error in errors)        
            {
                Console.WriteLine($"ERROR is {error.Message} {error.GetType()}");        
            }
        }
    }

}