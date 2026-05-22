// Copyright (c) 2011 by Henon <meinrad.recheis@gmail.com>
using Jint;
using System;
using System.Collections.Generic;

public interface IScriptInterpreter
{
    TimeSpan ExecutionTimeout { get; set; }
    bool Execute(string script);
    bool Execute(string script, out object result);
    void SetVariables(Dictionary<string, object> variables);
    void SetVariable(string name, object value);
    void SetFunction(string name, Delegate function);
}

public class JavaScriptInterpreter : IScriptInterpreter
{
    private readonly Engine m_engine;

    public JavaScriptInterpreter()
    {
        m_engine = new Engine();
    }

    public TimeSpan ExecutionTimeout { get; set; }

    public bool Execute(string script)
        // <-- discard
        => Execute(script, out object result);

    public bool Execute(string script, out object result)
    {
        result = m_engine.Execute(script);
        return true;
    }

    public void SetVariables(Dictionary<string, object> variables)
    {
        foreach (KeyValuePair<string, object> pair in variables)
        {
            SetVariable(pair.Key, pair.Value);
        }
    }

    public void SetVariable(string name, object value) => m_engine.SetValue(name, value);

    public void SetFunction(string name, Delegate function) => m_engine.SetValue(name, function);
}

