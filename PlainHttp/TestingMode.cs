﻿namespace PlainHttp;

public class TestingMode
{
    public Queue<HttpResponseMessage> RequestsQueue { get; set; }

    public TestingMode()
    {
        this.RequestsQueue = new Queue<HttpResponseMessage>();
    }
}
