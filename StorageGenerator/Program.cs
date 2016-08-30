﻿
// Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.CodeDom;
using System.IO;
using PInvoke;
using PInvoke.Parser;
using PInvoke.Transform;

internal static class Program
{

    /// <summary>
    /// Dll's which are somewhat more troublesome and should not be loaded by default
    /// </summary>
    /// <value></value>
    /// <returns></returns>
    /// <remarks></remarks>
    public static IEnumerable<string> FullDllList
    {
        get
        {
            List<string> list = new List<string>(ProcedureFinder.DefaultDllList);
            list.Add("oleaut32.dll");
            list.Add("ole32.dll");
            list.Add("ole2.dll");
            list.Add("ole2disp.dll");
            list.Add("ole2nls.dll");
            list.Add("msvcr80.dll");
            list.Add("nt64.dll");
            list.Add("msimg32.dll");
            list.Add("winscard.dll");
            list.Add("winspool.dll");
            list.Add("comctl32.dll");
            return list;
        }
    }

    private static NativeStorage CreateInitialNativeStorage()
    {
        NativeStorage ns = new NativeStorage();

        // Add in the basic type defs
        ns.AddTypedef(new NativeTypeDef("SIZE_T", new NativeBuiltinType(BuiltinType.NativeInt32, true)));
        ns.AddTypedef(new NativeTypeDef("DWORD64", new NativeBuiltinType(BuiltinType.NativeInt64, true)));
        ns.AddTypedef(new NativeTypeDef("HWND", new NativePointer(BuiltinType.NativeVoid)));
        ns.AddTypedef(new NativeTypeDef("HMENU", new NativePointer(BuiltinType.NativeVoid)));
        ns.AddTypedef(new NativeTypeDef("HACCEL", new NativePointer(BuiltinType.NativeVoid)));
        ns.AddTypedef(new NativeTypeDef("HBRUSH", new NativePointer(BuiltinType.NativeVoid)));
        ns.AddTypedef(new NativeTypeDef("HFONT", new NativePointer(BuiltinType.NativeVoid)));
        ns.AddTypedef(new NativeTypeDef("HDC", new NativePointer(BuiltinType.NativeVoid)));
        ns.AddTypedef(new NativeTypeDef("HICON", new NativePointer(BuiltinType.NativeVoid)));

        return ns;
    }

    /// <summary>
    /// Verification of the generated code
    /// </summary>
    /// <param name="ns"></param>
    /// <remarks></remarks>

    private static void VerifyGeneratedStorage(NativeStorage ns)
    {
        NativeProcedure proc = null;
        VerifyTrue(ns.TryFindProcedure("SendMessageA", out proc));
        VerifyTrue(ns.TryFindProcedure("SendMessageW", out proc));
        VerifyTrue(ns.TryFindProcedure("GetForegroundWindow", out proc));
        VerifyTrue(ns.TryFindProcedure("CreateWellKnownSid", out proc));

        NativeTypeDef typedef = null;
        VerifyTrue(ns.TryFindTypedef("LPCSTR", out typedef));
        VerifyTrue(ns.TryFindTypedef("LPWSTR", out typedef));

        NativeType defined = null;
        VerifyTrue(ns.TryFindByName("WNDPROC", out defined));
        VerifyTrue(ns.TryFindByName("HOOKPROC", out defined));
        VerifyTrue(ns.TryFindByName("tagPOINT", out defined));
        VerifyTrue(ns.TryFindByName("_SYSTEM_INFO", out defined));

        NativeConstant c = null;
        VerifyTrue(ns.TryFindConstant("WM_PAINT", out c));
        VerifyTrue(ns.TryFindConstant("WM_LBUTTONDOWN", out c));

    }

    private static void VerifyTrue(bool value)
    {
        if (!value)
        {
            throw new Exception();
        }
    }

    private static NativeStorage Generate(TextWriter writer)
    {
        NativeCodeAnalyzer analyzer = NativeCodeAnalyzerFactory.Create(OsVersion.WindowsVista);
        analyzer.IncludePathList.AddRange(NativeCodeAnalyzerFactory.GetCommonSdkPaths());

        // Run the preprocessor
        analyzer.Trace = true;
        string winPath = Path.Combine(PInvoke.Parser.NativeCodeAnalyzerFactory.GetPlatformSdkIncludePath(), "windows.h");
        TextReaderBag tr = analyzer.RunPreProcessor(winPath);
        File.WriteAllText("d:\\temp\\windows.out.h", tr.TextReader.ReadToEnd());
        analyzer.Trace = false;

        NativeCodeAnalyzerResult result = analyzer.Analyze(winPath);
        ErrorProvider ep = result.ErrorProvider;
        if (ep.Errors.Count > 0)
        {
            Debug.Fail("Encountered an error during the parse");
        }
        NativeSymbolBag bag = NativeSymbolBag.CreateFrom(result, CreateInitialNativeStorage(), ep);

        // Resolve with the full dll list
        using (ProcedureFinder finder = new ProcedureFinder(FullDllList))
        {
            bag.TryResolveSymbolsAndValues(finder, ep);
        }

        foreach (string msg in ep.AllMessages)
        {
            writer.WriteLine("' " + msg);
        }

        // GenerateCode(writer, bag)

        // Now write out the file
        var ns = new NativeStorage();
        ns.CacheLookup = true;
        bag.SaveToNativeStorage(ns);
        ns.CacheLookup = false;
        ns.AcceptChanges();
        VerifyGeneratedStorage(ns);
        ns.WriteXml("windows.xml");

        // Copy the file to the various applications
        File.Copy("windows.xml", "..\\..\\..\\ConsoleTool\\bin\\Debug\\windows.xml", true);
        File.Copy("windows.xml", "..\\..\\Data\\windows.xml", true);

        string fullInstallTarget = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), Path.Combine(PInvoke.Constants.ProductName, "Data\\windows.xml"));
        if (File.Exists(fullInstallTarget))
        {
            File.Copy("windows.xml", fullInstallTarget, true);
        }

        return ns;
    }


    public static void Main()
    {
        using (StreamWriter sw = new StreamWriter("d:\\temp\\windows.vb"))
        {
            Generate(sw);
        }

    }

}
