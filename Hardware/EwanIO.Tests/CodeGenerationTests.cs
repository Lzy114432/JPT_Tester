using System;
using System.IO;
using System.Linq;
using EwanIO.Core.Interfaces;
using EwanIO.Core.Attributes;
using EwanIO.Core.Context;
using EwanIO.Core.Data;
using EwanIO.Core.EdgeDetection;
using EwanIO.Core.Mapping;
using EwanIO.Core.Metadata;
using EwanIO.Core.Simulation;
using EwanIO.Core.CodeGen;
using Xunit;

namespace EwanIO.Tests
{
    public class CodeGenerationTests : IDisposable
    {
        private readonly string _testOutputDir;

        public CodeGenerationTests()
        {
            _testOutputDir = Path.Combine(Path.GetTempPath(), $"EwanIO_CodeGen_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testOutputDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testOutputDir))
            {
                try
                {
                    Directory.Delete(_testOutputDir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region Test Layout

        public class TestLayout
        {
            [IO(0)]
            public InputSignal 启动按钮 { get; set; }

            [IO(1, ConfirmTimeoutMs = 300)]
            public InputSignal 急停 { get; set; }

            [IO(2)]
            public InputSignal Sensor1 { get; set; }

            [IO(0)]
            public OutputSignal 运行灯 { get; set; }

            [IO(1)]
            public OutputSignal Output1 { get; set; }
        }

        #endregion

        #region Constants Generation Tests

        [Fact]
        public void LayoutCodeGenerator_GenerateConstants_ShouldProduceValidCode()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(TestLayout), new CodeGenOptions
            {
                Namespace = "EwanIO.Tests.Generated"
            });

            // Act
            string code = generator.GenerateConstants();

            // Assert
            Assert.Contains("namespace EwanIO.Tests.Generated", code);
            Assert.Contains("public static class TestLayoutIndices", code);
            Assert.Contains("public const int 启动按钮 = 0;", code);
            Assert.Contains("public const int 急停 = 1;", code);
            Assert.Contains("public const int Sensor1 = 2;", code);
            Assert.Contains("public const int Out_运行灯 = 0;", code);
            Assert.Contains("public const int Out_Output1 = 1;", code);
        }

        [Fact]
        public void LayoutCodeGenerator_GenerateConstants_WithoutNamespace_ShouldNotIncludeNamespace()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(TestLayout), new CodeGenOptions
            {
                Namespace = null
            });

            // Act
            string code = generator.GenerateConstants();

            // Assert
            Assert.DoesNotContain("namespace", code);
            Assert.Contains("public static class TestLayoutIndices", code);
        }

        [Fact]
        public void LayoutCodeGenerator_GenerateConstants_ShouldIncludeRegions()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(TestLayout));

            // Act
            string code = generator.GenerateConstants();

            // Assert
            Assert.Contains("#region Input Indices", code);
            Assert.Contains("#region Output Indices", code);
            Assert.Contains("#endregion", code);
        }

        #endregion

        #region Accessors Generation Tests

        [Fact]
        public void LayoutCodeGenerator_GenerateAccessors_ShouldProduceValidCode()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(TestLayout), new CodeGenOptions
            {
                Namespace = "EwanIO.Tests.Generated"
            });

            // Act
            string code = generator.GenerateAccessors();

            // Assert
            Assert.Contains("namespace EwanIO.Tests.Generated", code);
            Assert.Contains("public static class TestLayoutAccessors", code);
            Assert.Contains("using EwanIO.Core.Context;", code);
        }

        [Fact]
        public void LayoutCodeGenerator_GenerateAccessors_ShouldIncludeInputAccessors()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(TestLayout));

            // Act
            string code = generator.GenerateAccessors();

            // Assert
            Assert.Contains("public static bool Get启动按钮(this IoContext<TestLayout> ctx)", code);
            Assert.Contains("return ctx.GetInput(0);", code);
            Assert.Contains("public static bool GetSensor1(this IoContext<TestLayout> ctx)", code);
            Assert.Contains("return ctx.GetInput(2);", code);
        }

        [Fact]
        public void LayoutCodeGenerator_GenerateAccessors_ShouldIncludeOutputAccessors()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(TestLayout));

            // Act
            string code = generator.GenerateAccessors();

            // Assert
            // Set 方法
            Assert.Contains("public static void Set运行灯(this IoContext<TestLayout> ctx, bool value, bool now = false)", code);
            Assert.Contains("if (value)", code);
            Assert.Contains("ctx.On(0, now);", code);
            Assert.Contains("ctx.Off(0, now);", code);

            // On/Off 便捷方法
            Assert.Contains("public static void Set运行灯On(this IoContext<TestLayout> ctx, bool now = false)", code);
            Assert.Contains("ctx.On(0, now);", code);
            Assert.Contains("public static void Set运行灯Off(this IoContext<TestLayout> ctx, bool now = false)", code);
            Assert.Contains("ctx.Off(0, now);", code);
        }

        #endregion

        #region Documentation Generation Tests

        [Fact]
        public void LayoutCodeGenerator_GenerateMarkdownDoc_ShouldProduceValidMarkdown()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(TestLayout));

            // Act
            string doc = generator.GenerateMarkdownDoc();

            // Assert
            Assert.Contains("# TestLayout IO 文档", doc);
            Assert.Contains("## 输入 (Inputs) - 共 3 个", doc);
            Assert.Contains("## 输出 (Outputs) - 共 2 个", doc);
            Assert.Contains("| 索引 | 名称 | 类型 |", doc);
            Assert.Contains("| 0 | 启动按钮 | Input |", doc);
            Assert.Contains("| 1 | 急停 | Input | 超时: 300ms |", doc);
            Assert.Contains("| 0 | 运行灯 | Output |", doc);
        }

        [Fact]
        public void LayoutCodeGenerator_GenerateMarkdownDoc_ShouldIncludeExampleCode()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(TestLayout));

            // Act
            string doc = generator.GenerateMarkdownDoc();

            // Assert
            Assert.Contains("## 示例代码", doc);
            Assert.Contains("```csharp", doc);
            Assert.Contains("var ctx = IoContextBuilder.For<TestLayout>()", doc);
            Assert.Contains("```", doc);
        }

        #endregion

        #region File Generation Tests

        [Fact]
        public void LayoutCodeGenerator_GenerateAll_ShouldCreateAllFiles()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(TestLayout), new CodeGenOptions
            {
                Namespace = "EwanIO.Tests.Generated",
                GenerateConstants = true,
                GenerateAccessors = true,
                GenerateDocumentation = true
            });

            // Act
            var result = generator.GenerateAll(_testOutputDir);

            // Assert
            Assert.NotNull(result.ConstantsFile);
            Assert.NotNull(result.AccessorsFile);
            Assert.NotNull(result.DocumentationFile);

            Assert.True(File.Exists(result.ConstantsFile));
            Assert.True(File.Exists(result.AccessorsFile));
            Assert.True(File.Exists(result.DocumentationFile));

            Assert.Equal(3, result.AllFiles.Count());
        }

        [Fact]
        public void LayoutCodeGenerator_GenerateAll_WithSelectiveOptions_ShouldCreateSelectedFiles()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(TestLayout), new CodeGenOptions
            {
                GenerateConstants = true,
                GenerateAccessors = false,
                GenerateDocumentation = true
            });

            // Act
            var result = generator.GenerateAll(_testOutputDir);

            // Assert
            Assert.NotNull(result.ConstantsFile);
            Assert.Null(result.AccessorsFile);
            Assert.NotNull(result.DocumentationFile);

            Assert.True(File.Exists(result.ConstantsFile));
            Assert.True(File.Exists(result.DocumentationFile));

            Assert.Equal(2, result.AllFiles.Count());
        }

        [Fact]
        public void LayoutCodeGenerator_GenerateAll_ShouldCreateOutputDirectory()
        {
            // Arrange
            string newDir = Path.Combine(_testOutputDir, "NewSubDir");
            var generator = new LayoutCodeGenerator(typeof(TestLayout));

            // Act
            var result = generator.GenerateAll(newDir);

            // Assert
            Assert.True(Directory.Exists(newDir));
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void LayoutCodeGenerator_Constructor_WithNullType_ShouldThrow()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new LayoutCodeGenerator(null!));
        }

        [Fact]
        public void LayoutCodeGenerator_GenerateAll_WithNullDirectory_ShouldThrow()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(TestLayout));

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => generator.GenerateAll(null!));
        }

        #endregion

        #region Special Cases Tests

        public class EmptyLayout
        {
            // No IO attributes
        }

        [Fact]
        public void LayoutCodeGenerator_WithEmptyLayout_ShouldGenerateEmptyConstants()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(EmptyLayout));

            // Act
            string code = generator.GenerateConstants();

            // Assert
            Assert.Contains("public static class EmptyLayoutIndices", code);
            Assert.DoesNotContain("#region Input Indices", code);
            Assert.DoesNotContain("#region Output Indices", code);
        }

        public class NumericStartLayout
        {
            [IO(0)]
            public InputSignal _1号传感器 { get; set; }  // 以下划线开头（合法标识符）
        }

        [Fact]
        public void LayoutCodeGenerator_WithNumericStartProperty_ShouldKeepValidIdentifier()
        {
            // Arrange
            var generator = new LayoutCodeGenerator(typeof(NumericStartLayout));

            // Act
            string code = generator.GenerateConstants();

            // Assert - 属性名本身已经是合法标识符，不需要额外处理
            Assert.Contains("public const int _1号传感器 = 0;", code);
        }

        #endregion
    }
}
