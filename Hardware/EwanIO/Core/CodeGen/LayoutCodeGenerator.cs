using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using EwanIO.Core.Attributes;
using EwanIO.Core.Metadata;

namespace EwanIO.Core.CodeGen
{
    /// <summary>
    /// Layout 代码生成器
    /// 功能：
    /// - 生成常量索引类（避免魔法数字）
    /// - 生成强类型访问器（性能优化场景）
    /// - 生成文档（Markdown/XML）
    /// </summary>
    public class LayoutCodeGenerator
    {
        private readonly Type _layoutType;
        private readonly List<IoMeta> _inputMetas;
        private readonly List<IoMeta> _outputMetas;
        private readonly CodeGenOptions _options;

        public LayoutCodeGenerator(Type layoutType, CodeGenOptions? options = null)
        {
            if (layoutType == null)
                throw new ArgumentNullException(nameof(layoutType));

            _layoutType = layoutType;
            _options = options ?? new CodeGenOptions();

            // 扫描 Layout 类型的 IO 属性
            _inputMetas = new List<IoMeta>();
            _outputMetas = new List<IoMeta>();
            ScanLayoutType();
        }

        /// <summary>
        /// 扫描 Layout 类型的 IO 属性
        /// </summary>
        private void ScanLayoutType()
        {
            foreach (var prop in _layoutType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<IOAttribute>();
                if (attr == null) continue;

                var meta = new IoMeta(attr.Index, prop.Name, attr.DisplayName, attr.ConfirmTimeoutMs, prop);

                if (prop.PropertyType == typeof(InputSignal))
                {
                    _inputMetas.Add(meta);
                }
                else if (prop.PropertyType == typeof(OutputSignal))
                {
                    _outputMetas.Add(meta);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"IO property {prop.Name} must be of type InputSignal or OutputSignal");
                }
            }
        }

        /// <summary>
        /// 生成常量索引类
        /// </summary>
        public string GenerateConstants()
        {
            var sb = new StringBuilder();

            // 命名空间和类声明
            sb.AppendLine("using System;");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(_options.Namespace))
            {
                sb.AppendLine($"namespace {_options.Namespace}");
                sb.AppendLine("{");
            }

            // 类注释
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {_layoutType.Name} 的 IO 索引常量");
            sb.AppendLine($"    /// 自动生成 - 不要手动修改");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static class {_layoutType.Name}Indices");
            sb.AppendLine("    {");

            // 输入常量
            if (_inputMetas.Count > 0)
            {
                sb.AppendLine("        #region Input Indices");
                sb.AppendLine();
                foreach (var input in _inputMetas.OrderBy(m => m.Index))
                {
                    sb.AppendLine($"        /// <summary>{input.PropertyName} (Input {input.Index})</summary>");
                    sb.AppendLine($"        public const int {SanitizeIdentifier(input.PropertyName)} = {input.Index};");
                }
                sb.AppendLine();
                sb.AppendLine("        #endregion");
                sb.AppendLine();
            }

            // 输出常量
            if (_outputMetas.Count > 0)
            {
                sb.AppendLine("        #region Output Indices");
                sb.AppendLine();
                foreach (var output in _outputMetas.OrderBy(m => m.Index))
                {
                    sb.AppendLine($"        /// <summary>{output.PropertyName} (Output {output.Index})</summary>");
                    sb.AppendLine($"        public const int Out_{SanitizeIdentifier(output.PropertyName)} = {output.Index};");
                }
                sb.AppendLine();
                sb.AppendLine("        #endregion");
            }

            sb.AppendLine("    }");

            if (!string.IsNullOrEmpty(_options.Namespace))
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 生成强类型访问器（扩展方法）
        /// </summary>
        public string GenerateAccessors()
        {
            var sb = new StringBuilder();

            // 命名空间和类声明
            sb.AppendLine("using System;");
            sb.AppendLine("using EwanIO.Core.Context;");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(_options.Namespace))
            {
                sb.AppendLine($"namespace {_options.Namespace}");
                sb.AppendLine("{");
            }

            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {_layoutType.Name} 的强类型访问器");
            sb.AppendLine($"    /// 自动生成 - 不要手动修改");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static class {_layoutType.Name}Accessors");
            sb.AppendLine("    {");

            // 输入访问器
            if (_inputMetas.Count > 0)
            {
                sb.AppendLine("        #region Input Accessors");
                sb.AppendLine();
                foreach (var input in _inputMetas.OrderBy(m => m.Index))
                {
                    string methodName = $"Get{SanitizeIdentifier(input.PropertyName)}";
                    sb.AppendLine($"        /// <summary>读取 {input.PropertyName} (Input {input.Index})</summary>");
                    sb.AppendLine($"        public static bool {methodName}(this IoContext<{_layoutType.Name}> ctx)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            return ctx.GetInput({input.Index});");
                    sb.AppendLine($"        }}");
                    sb.AppendLine();
                }
                sb.AppendLine("        #endregion");
                sb.AppendLine();
            }

            // 输出访问器
            if (_outputMetas.Count > 0)
            {
                sb.AppendLine("        #region Output Accessors");
                sb.AppendLine();
                foreach (var output in _outputMetas.OrderBy(m => m.Index))
                {
                    string methodName = $"Set{SanitizeIdentifier(output.PropertyName)}";
                    sb.AppendLine($"        /// <summary>设置 {output.PropertyName} (Output {output.Index})</summary>");
                    sb.AppendLine($"        public static void {methodName}(this IoContext<{_layoutType.Name}> ctx, bool value, bool now = false)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            if (value)");
                    sb.AppendLine($"            {{");
                    sb.AppendLine($"                ctx.On({output.Index}, now);");
                    sb.AppendLine($"            }}");
                    sb.AppendLine($"            else");
                    sb.AppendLine($"            {{");
                    sb.AppendLine($"                ctx.Off({output.Index}, now);");
                    sb.AppendLine($"            }}");
                    sb.AppendLine($"        }}");
                    sb.AppendLine();

                    // 生成便捷方法
                    string onMethodName = $"{methodName}On";
                    string offMethodName = $"{methodName}Off";
                    sb.AppendLine($"        /// <summary>打开 {output.PropertyName} (Output {output.Index})</summary>");
                    sb.AppendLine($"        public static void {onMethodName}(this IoContext<{_layoutType.Name}> ctx, bool now = false)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            ctx.On({output.Index}, now);");
                    sb.AppendLine($"        }}");
                    sb.AppendLine();

                    sb.AppendLine($"        /// <summary>关闭 {output.PropertyName} (Output {output.Index})</summary>");
                    sb.AppendLine($"        public static void {offMethodName}(this IoContext<{_layoutType.Name}> ctx, bool now = false)");
                    sb.AppendLine($"        {{");
                    sb.AppendLine($"            ctx.Off({output.Index}, now);");
                    sb.AppendLine($"        }}");
                    sb.AppendLine();
                }
                sb.AppendLine("        #endregion");
            }

            sb.AppendLine("    }");

            if (!string.IsNullOrEmpty(_options.Namespace))
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 生成 Markdown 文档
        /// </summary>
        public string GenerateMarkdownDoc()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# {_layoutType.Name} IO 文档");
            sb.AppendLine();
            sb.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 输入文档
            if (_inputMetas.Count > 0)
            {
                sb.AppendLine($"## 输入 (Inputs) - 共 {_inputMetas.Count} 个");
                sb.AppendLine();
                sb.AppendLine("| 索引 | 名称 | 类型 | 备注 |");
                sb.AppendLine("|------|------|------|------|");
                foreach (var input in _inputMetas.OrderBy(m => m.Index))
                {
                    string timeout = input.ConfirmTimeoutMs > 0 ? $"{input.ConfirmTimeoutMs}ms" : "-";
                    sb.AppendLine($"| {input.Index} | {input.PropertyName} | Input | 超时: {timeout} |");
                }
                sb.AppendLine();
            }

            // 输出文档
            if (_outputMetas.Count > 0)
            {
                sb.AppendLine($"## 输出 (Outputs) - 共 {_outputMetas.Count} 个");
                sb.AppendLine();
                sb.AppendLine("| 索引 | 名称 | 类型 |");
                sb.AppendLine("|------|------|------|");
                foreach (var output in _outputMetas.OrderBy(m => m.Index))
                {
                    sb.AppendLine($"| {output.Index} | {output.PropertyName} | Output |");
                }
                sb.AppendLine();
            }

            // 示例代码
            sb.AppendLine("## 示例代码");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            sb.AppendLine($"// 创建 IoContext");
            sb.AppendLine($"var ctx = IoContextBuilder.For<{_layoutType.Name}>()");
            sb.AppendLine($"    .WithId(\"MyContext\")");
            sb.AppendLine($"    .WithHardware(hardware)");
            sb.AppendLine($"    .Build();");
            sb.AppendLine();
            sb.AppendLine($"// 读取输入");
            if (_inputMetas.Count > 0)
            {
                var firstInput = _inputMetas.OrderBy(m => m.Index).First();
                sb.AppendLine($"bool value = ctx.GetInput({firstInput.Index}); // {firstInput.PropertyName}");
            }
            sb.AppendLine();
            sb.AppendLine($"// 设置输出");
            if (_outputMetas.Count > 0)
            {
                var firstOutput = _outputMetas.OrderBy(m => m.Index).First();
                sb.AppendLine($"ctx.On({firstOutput.Index}); // {firstOutput.PropertyName}");
            }
            sb.AppendLine("```");

            return sb.ToString();
        }

        /// <summary>
        /// 生成所有代码文件到指定目录
        /// </summary>
        public GeneratedFiles GenerateAll(string outputDirectory)
        {
            if (string.IsNullOrEmpty(outputDirectory))
                throw new ArgumentNullException(nameof(outputDirectory));

            if (!System.IO.Directory.Exists(outputDirectory))
                System.IO.Directory.CreateDirectory(outputDirectory);

            var result = new GeneratedFiles();

            // 生成常量类
            if (_options.GenerateConstants)
            {
                string constantsCode = GenerateConstants();
                string constantsPath = System.IO.Path.Combine(outputDirectory, $"{_layoutType.Name}Indices.cs");
                System.IO.File.WriteAllText(constantsPath, constantsCode);
                result.ConstantsFile = constantsPath;
            }

            // 生成访问器
            if (_options.GenerateAccessors)
            {
                string accessorsCode = GenerateAccessors();
                string accessorsPath = System.IO.Path.Combine(outputDirectory, $"{_layoutType.Name}Accessors.cs");
                System.IO.File.WriteAllText(accessorsPath, accessorsCode);
                result.AccessorsFile = accessorsPath;
            }

            // 生成文档
            if (_options.GenerateDocumentation)
            {
                string docCode = GenerateMarkdownDoc();
                string docPath = System.IO.Path.Combine(outputDirectory, $"{_layoutType.Name}.md");
                System.IO.File.WriteAllText(docPath, docCode);
                result.DocumentationFile = docPath;
            }

            return result;
        }

        /// <summary>
        /// 清理标识符（移除不合法字符）
        /// </summary>
        private string SanitizeIdentifier(string name)
        {
            // 对于中文属性名，保持原样（C# 支持 Unicode 标识符）
            // 只需要确保不以数字开头
            if (string.IsNullOrEmpty(name))
                return "_";

            // 如果以数字开头，添加下划线前缀
            if (char.IsDigit(name[0]))
                return "_" + name;

            return name;
        }
    }

    /// <summary>
    /// 代码生成选项
    /// </summary>
    public class CodeGenOptions
    {
        /// <summary>
        /// 生成的代码命名空间
        /// </summary>
        public string? Namespace { get; set; }

        /// <summary>
        /// 是否生成常量类
        /// </summary>
        public bool GenerateConstants { get; set; } = true;

        /// <summary>
        /// 是否生成访问器
        /// </summary>
        public bool GenerateAccessors { get; set; } = true;

        /// <summary>
        /// 是否生成文档
        /// </summary>
        public bool GenerateDocumentation { get; set; } = true;
    }

    /// <summary>
    /// 生成的文件信息
    /// </summary>
    public class GeneratedFiles
    {
        public string? ConstantsFile { get; set; }
        public string? AccessorsFile { get; set; }
        public string? DocumentationFile { get; set; }

        public IEnumerable<string> AllFiles
        {
            get
            {
                if (ConstantsFile != null) yield return ConstantsFile;
                if (AccessorsFile != null) yield return AccessorsFile;
                if (DocumentationFile != null) yield return DocumentationFile;
            }
        }
    }
}
