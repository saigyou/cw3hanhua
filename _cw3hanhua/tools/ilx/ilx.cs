// ilx —— 基于 Mono.Cecil 的 IL 检查工具（只读，不修改任何文件）
//
// 为什么存在：il.py / ilscan.py / ilscan_field.py 是手写的 ECMA-335 解析器，
// 方法头（tiny/fat）、异常子句表、switch 跳转表、MethodSpec 泛型实例化都要自己算。
// 这些地方出过一次错就会给出错误结论（见 NOTES 4.4）。Cecil 是 BepInEx / Unity /
// ILSpy 共用的解析器，让这一整类错误在结构上消失。
//
// 用法：
//   ilx dump <Type> [方法名...]        精读方法 IL（不给方法名则列出该类型的全部方法）
//   ilx callers [--list] [--type T] <方法名...>   谁调用了它（call/callvirt/newobj）
//   ilx fields  [--type T] <字段名...>            谁直接读写它（ldfld/stfld/...）
//   ilx find <子串>                    按名字搜类型/方法/字段
//
// 通用选项：
//   --dll <路径>   指定程序集（默认游戏自带的 Assembly-CSharp.dll）
//   --type <名字>  只统计声明类型匹配的调用/字段
//
// 输出与 il.py 的差异（有意的）：
//   - 方法头多了 token / params / il / eh 计数
//   - 调用目标带参数表，泛型实例化会显示 <...>（il.py 只会打 tok2B:…）
//   - ldftn / ldvirtftn 仍不计入 callers（与 ilscan.py 口径一致，便于回归对比）
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class Ilx
{
    const string DefaultDll =
        @"C:\Program Files (x86)\Steam\steamapps\common\Creeper World 3\CW3_Data\Managed\Assembly-CSharp.dll";

    static string dllPath = DefaultDll;
    static string typeFilter = null;
    static bool quiet = false;

    static int Main(string[] args)
    {
        try { Console.OutputEncoding = new UTF8Encoding(false); }
        catch { }

        if (args.Length == 0) { Usage(); return 2; }

        List<string> rest = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "--dll" && i + 1 < args.Length) { dllPath = args[++i]; }
            else if (a == "--type" && i + 1 < args.Length) { typeFilter = args[++i]; }
            else if (a.StartsWith("--type=")) { typeFilter = a.Substring(7); }
            else if (a == "--list") { quiet = true; }
            else rest.Add(a);
        }
        if (rest.Count == 0) { Usage(); return 2; }

        string cmd = rest[0];
        rest.RemoveAt(0);

        AssemblyDefinition asm;
        try { asm = AssemblyDefinition.ReadAssembly(dllPath); }
        catch (Exception e)
        {
            Console.Error.WriteLine("无法读取程序集: " + dllPath);
            Console.Error.WriteLine(e.Message);
            return 1;
        }

        switch (cmd)
        {
            case "dump":    return Dump(asm, rest);
            case "callers": return Callers(asm, rest);
            case "fields":  return Fields(asm, rest);
            case "find":    return Find(asm, rest);
            case "stats":   return Stats(asm);
            default: Usage(); return 2;
        }
    }

    static void Usage()
    {
        Console.Error.WriteLine("ilx dump <Type> [方法名...]");
        Console.Error.WriteLine("ilx callers [--list] [--type T] <方法名...>");
        Console.Error.WriteLine("ilx fields [--type T] <字段名...>");
        Console.Error.WriteLine("ilx find <子串>");
        Console.Error.WriteLine("通用: --dll <路径>  --type <声明类型>");
    }

    // ---------- 类型/方法查找 ----------

    static bool TypeMatches(TypeReference t)
    {
        if (typeFilter == null) return true;
        if (t == null) return false;
        string n = ShortTypeName(t);          // 去掉 `1 这类泛型元数
        if (n == typeFilter) return true;
        if (t.FullName == typeFilter) return true;
        return t.FullName.EndsWith("." + typeFilter, StringComparison.Ordinal);
    }

    static string ShortTypeName(TypeReference t)
    {
        if (t == null) return "?";
        string n = t.Name;
        int tick = n.IndexOf('`');
        if (tick >= 0) n = n.Substring(0, tick);
        return n;
    }

    static MethodReference Unwrap(MethodReference m)
    {
        GenericInstanceMethod g = m as GenericInstanceMethod;
        return g != null ? g.ElementMethod : m;
    }

    static string FmtMethod(MethodReference m)
    {
        GenericInstanceMethod g = m as GenericInstanceMethod;
        MethodReference e = g != null ? g.ElementMethod : m;

        StringBuilder sb = new StringBuilder();
        sb.Append(ShortTypeName(e.DeclaringType)).Append('.').Append(e.Name);
        if (g != null && g.GenericArguments.Count > 0)
        {
            sb.Append('<');
            for (int i = 0; i < g.GenericArguments.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(ShortTypeName(g.GenericArguments[i]));
            }
            sb.Append('>');
        }
        sb.Append('(');
        for (int i = 0; i < e.Parameters.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(ShortTypeName(e.Parameters[i].ParameterType));
        }
        sb.Append(')');
        return sb.ToString();
    }

    static string Quote(string s)
    {
        StringBuilder sb = new StringBuilder("'");
        foreach (char c in s)
        {
            switch (c)
            {
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\'': sb.Append("\\'"); break;
                case '\\': sb.Append("\\\\"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('\'');
        return sb.ToString();
    }

    static string FmtOperand(Instruction ins)
    {
        object o = ins.Operand;
        if (o == null) return "";

        string s = o as string;
        if (s != null) return "STR:" + Quote(s);

        MethodReference mr = o as MethodReference;
        if (mr != null) return FmtMethod(mr);

        FieldReference fr = o as FieldReference;
        if (fr != null) return "fld:" + fr.Name;

        TypeReference tr = o as TypeReference;
        if (tr != null) return "type:" + ShortTypeName(tr);

        VariableDefinition vd = o as VariableDefinition;
        if (vd != null) return "V_" + vd.Index.ToString(CultureInfo.InvariantCulture);

        ParameterDefinition pd = o as ParameterDefinition;
        if (pd != null) return "A_" + pd.Index.ToString(CultureInfo.InvariantCulture);

        Instruction ti = o as Instruction;
        if (ti != null) return ti.Offset.ToString("X4", CultureInfo.InvariantCulture);

        Instruction[] arr = o as Instruction[];
        if (arr != null) return "switch[" + arr.Length.ToString(CultureInfo.InvariantCulture) + "]";

        return Convert.ToString(o, CultureInfo.InvariantCulture);
    }

    // ---------- dump ----------

    static int Dump(AssemblyDefinition asm, List<string> args)
    {
        if (args.Count == 0) { Usage(); return 2; }
        string tname = args[0];
        HashSet<string> names = new HashSet<string>();
        for (int i = 1; i < args.Count; i++) names.Add(args[i]);

        int found = 0;
        foreach (TypeDefinition t in AllTypes(asm.MainModule))
        {
            if (!(t.Name == tname || t.FullName == tname ||
                  t.FullName.EndsWith("." + tname, StringComparison.Ordinal))) continue;

            if (names.Count == 0)
            {
                Console.WriteLine("# " + t.FullName + "  方法数 = " + t.Methods.Count);
                foreach (MethodDefinition m in t.Methods)
                    Console.WriteLine("  " + m.Name + "  token=0x" +
                        m.MetadataToken.ToInt32().ToString("X8", CultureInfo.InvariantCulture));
                found++;
                continue;
            }

            foreach (MethodDefinition m in t.Methods)
            {
                if (!names.Contains(m.Name)) continue;
                found++;
                DumpMethod(t, m);
            }
        }
        if (found == 0)
        {
            Console.Error.WriteLine("未找到: " + tname +
                (names.Count > 0 ? " / " + string.Join(", ", ToArray(names)) : ""));
            return 1;
        }
        return 0;
    }

    static void DumpMethod(TypeDefinition t, MethodDefinition m)
    {
        Console.WriteLine();
        Console.Write("===== " + t.FullName + "." + m.Name);
        Console.Write("   token=0x" + m.MetadataToken.ToInt32().ToString("X8", CultureInfo.InvariantCulture));
        Console.Write("  RVA=0x" + m.RVA.ToString("X", CultureInfo.InvariantCulture));
        Console.Write("  params=" + m.Parameters.Count.ToString(CultureInfo.InvariantCulture));
        if (m.HasBody)
        {
            Console.Write("  il=" + m.Body.Instructions.Count.ToString(CultureInfo.InvariantCulture));
            Console.Write("  eh=" + m.Body.ExceptionHandlers.Count.ToString(CultureInfo.InvariantCulture));
        }
        Console.WriteLine(" =====");

        if (!m.HasBody)
        {
            Console.WriteLine("   （无方法体 / extern）");
            return;
        }

        foreach (Instruction ins in m.Body.Instructions)
        {
            string op = FmtOperand(ins);
            Console.WriteLine("   " + ins.Offset.ToString("X4", CultureInfo.InvariantCulture) +
                              ": " + ins.OpCode.Name + (op.Length > 0 ? " " + op : ""));
        }
    }

    // ---------- callers ----------

    static int Callers(AssemblyDefinition asm, List<string> args)
    {
        if (args.Count == 0) { Usage(); return 2; }
        HashSet<string> targets = new HashSet<string>(args);

        Dictionary<string, int> hits = new Dictionary<string, int>();
        List<string> detail = new List<string>();
        int nMethods = 0;

        foreach (MethodDefinition m in AllMethods(asm.MainModule))
        {
            if (!m.HasBody) continue;
            nMethods++;
            string caller = m.DeclaringType.Name + "." + m.Name;

            foreach (Instruction ins in m.Body.Instructions)
            {
                OpCode oc = ins.OpCode;
                if (oc != OpCodes.Call && oc != OpCodes.Callvirt && oc != OpCodes.Newobj) continue;

                MethodReference mr = ins.Operand as MethodReference;
                if (mr == null) continue;
                MethodReference e = Unwrap(mr);
                if (e.DeclaringType == null) continue;
                if (!targets.Contains(e.Name)) continue;
                if (!TypeMatches(e.DeclaringType)) continue;

                string key = caller + "\u0001" + FmtMethodNoParams(mr) + "\u0001" + oc.Name;
                int c;
                hits.TryGetValue(key, out c);
                hits[key] = c + 1;
                detail.Add(caller + "  ->  " + oc.Name + " " + FmtMethod(mr));
            }
        }

        Console.WriteLine("# ilx callers " + string.Join(", ", ToArray(targets)) +
                          "   扫描方法数 = " + nMethods.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("# 命中 " + detail.Count.ToString(CultureInfo.InvariantCulture) + " 处");
        Console.WriteLine();

        List<KeyValuePair<string, int>> rows = new List<KeyValuePair<string, int>>(hits);
        rows.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
        {
            int r = b.Value.CompareTo(a.Value);
            return r != 0 ? r : string.CompareOrdinal(a.Key, b.Key);
        });
        foreach (KeyValuePair<string, int> kv in rows)
        {
            string[] parts = kv.Key.Split('\u0001');
            Console.WriteLine("  " + kv.Value.ToString(CultureInfo.InvariantCulture).PadLeft(3) +
                              "x  " + parts[0] + "  ->  " + parts[2] + " " + parts[1]);
        }

        if (!quiet)
        {
            Console.WriteLine();
            Console.WriteLine("--- 明细（按方法内出现顺序）---");
            foreach (string d in detail) Console.WriteLine("  " + d);
        }
        return 0;
    }

    static string FmtMethodNoParams(MethodReference m)
    {
        string s = FmtMethod(m);
        int p = s.IndexOf('(');
        return p >= 0 ? s.Substring(0, p) : s;
    }

    static TypeDefinition SafeResolveType(TypeReference t)
    {
        try { return t.Resolve(); }
        catch { return null; }
    }

    // ---------- fields ----------

    static int Fields(AssemblyDefinition asm, List<string> args)
    {
        if (args.Count == 0) { Usage(); return 2; }
        HashSet<string> targets = new HashSet<string>(args);

        Dictionary<string, int> hits = new Dictionary<string, int>();
        int nMethods = 0;

        foreach (MethodDefinition m in AllMethods(asm.MainModule))
        {
            if (!m.HasBody) continue;
            nMethods++;
            string caller = m.DeclaringType.Name + "." + m.Name;

            foreach (Instruction ins in m.Body.Instructions)
            {
                OpCode oc = ins.OpCode;
                if (oc != OpCodes.Ldfld && oc != OpCodes.Ldflda && oc != OpCodes.Stfld &&
                    oc != OpCodes.Ldsfld && oc != OpCodes.Ldsflda && oc != OpCodes.Stsfld) continue;

                FieldReference fr = ins.Operand as FieldReference;
                if (fr == null) continue;
                if (!targets.Contains(fr.Name)) continue;
                if (!TypeMatches(fr.DeclaringType)) continue;

                string key = caller + "\u0001" + oc.Name + "\u0001" + fr.Name;
                int c;
                hits.TryGetValue(key, out c);
                hits[key] = c + 1;
            }
        }

        Console.WriteLine("# ilx fields " + string.Join(", ", ToArray(targets)));
        Console.WriteLine("# 扫描方法数 = " + nMethods.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("# 直接读写这些字段的方法（绕过属性 setter 的写法）");
        Console.WriteLine();

        List<KeyValuePair<string, int>> rows = new List<KeyValuePair<string, int>>(hits);
        rows.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
        {
            int r = b.Value.CompareTo(a.Value);
            return r != 0 ? r : string.CompareOrdinal(a.Key, b.Key);
        });

        if (rows.Count == 0) Console.WriteLine("  （没有任何方法直接读写该字段）");
        foreach (KeyValuePair<string, int> kv in rows)
        {
            string[] parts = kv.Key.Split('\u0001');
            Console.WriteLine("  " + kv.Value.ToString(CultureInfo.InvariantCulture).PadLeft(3) +
                              "x  " + parts[0].PadRight(40) + " -> " + parts[1] + " " + parts[2]);
        }
        return 0;
    }

    // ---------- stats ----------

    static int Stats(AssemblyDefinition asm)
    {
        int n = 0;
        foreach (MethodDefinition m in AllMethods(asm.MainModule))
        {
            if (!m.HasBody) continue;
            n++;
            Console.WriteLine("0x" + m.MetadataToken.ToInt32().ToString("X8", CultureInfo.InvariantCulture) +
                "\t" + m.DeclaringType.Name + "." + m.Name +
                "\tcode=" + m.Body.CodeSize.ToString(CultureInfo.InvariantCulture) +
                "\til=" + m.Body.Instructions.Count.ToString(CultureInfo.InvariantCulture) +
                "\teh=" + m.Body.ExceptionHandlers.Count.ToString(CultureInfo.InvariantCulture));
        }
        Console.Error.WriteLine("# 有 IL 体的方法数 = " + n.ToString(CultureInfo.InvariantCulture));
        return 0;
    }

    // ---------- find ----------

    static int Find(AssemblyDefinition asm, List<string> args)
    {
        if (args.Count == 0) { Usage(); return 2; }
        string needle = string.Join(" ", ToArray(args));
        int n = 0;

        foreach (TypeDefinition t in AllTypes(asm.MainModule))
        {
            if (t.FullName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("type    " + t.FullName + "  token=0x" +
                    t.MetadataToken.ToInt32().ToString("X8", CultureInfo.InvariantCulture));
                n++;
            }
            foreach (MethodDefinition m in t.Methods)
            {
                if (m.FullName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine("method  " + m.FullName + "  token=0x" +
                        m.MetadataToken.ToInt32().ToString("X8", CultureInfo.InvariantCulture));
                    n++;
                }
            }
            foreach (FieldDefinition f in t.Fields)
            {
                if (f.FullName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine("field   " + f.FullName);
                    n++;
                }
            }
        }
        Console.WriteLine("# 命中 " + n.ToString(CultureInfo.InvariantCulture) + " 项");
        return n > 0 ? 0 : 1;
    }

    // ---------- 遍历 ----------

    static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition mod)
    {
        foreach (TypeDefinition t in mod.Types)
            foreach (TypeDefinition x in WalkType(t))
                yield return x;
    }

    static IEnumerable<TypeDefinition> WalkType(TypeDefinition t)
    {
        yield return t;
        foreach (TypeDefinition n in t.NestedTypes)
            foreach (TypeDefinition x in WalkType(n))
                yield return x;
    }

    static IEnumerable<MethodDefinition> AllMethods(ModuleDefinition mod)
    {
        foreach (TypeDefinition t in AllTypes(mod))
            foreach (MethodDefinition m in t.Methods)
                yield return m;
    }

    static string[] ToArray(ICollection<string> c)
    {
        string[] a = new string[c.Count];
        c.CopyTo(a, 0);
        return a;
    }
}
