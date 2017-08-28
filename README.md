# LuciClient.NET
C# client library for Luci.

## Example 1 

Arbitrary anonymous object; binary attachments being included via Attachment class

```C#
Client cl = new Client();
cl.connect(host, port);
Attachment attachment1 = new ArrayAttachment("txt", Encoding.UTF8.GetBytes(string.Join("\n", list_of_strings)));
Attachment attachment2 = new ArrayAttachment("txt", Encoding.UTF8.GetBytes(anotherString));
Attachment attachment3 = new ArrayAttachment("bin", byteArray);

Message message = cl.sendMessageAndReceiveResults(new Message(new
{
    run = "YourService",
    inputA = new
    {
        objA = new { listOfString = attachment1, anotherString = attachment2 },
        objB = new { binary = attachment3 },
    },
    inputB = new
    {
        inputBInt = 17,
        inputBString = "Hello World",
        subObject = new 
        {
            level3 = "value",
            list = new List<string>{ "hello","world"};
        }
    }
}));
```
