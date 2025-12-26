using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EwanCore.AlarmSystem;
using Xunit;

namespace EwanCommon.Tests
{
    /// <summary>
    /// AlarmService 单元测试
    /// </summary>
    public class AlarmServiceTests
    {
        #region 基本报警添加测试

        [Fact]
        public void AddAlarm_SimpleContent_ShouldAddAlarm()
        {
            // Arrange
            var service = new AlarmService();

            // Act
            service.AddAlarm("测试报警");

            // Assert
            Assert.Equal(1, service.AlarmCount);
            Assert.True(service.HasAlarm);
            var alarm = service.Snapshot.First();
            Assert.Equal("测试报警", alarm.Content);
            Assert.Equal("测试报警", alarm.Key); // Key 默认使用 Content
        }

        [Fact]
        public void AddAlarm_WithAllParameters_ShouldSetAllProperties()
        {
            // Arrange
            var service = new AlarmService();
            var owner = new object();

            // Act
            service.AddAlarm("报警内容", AlarmLevel.H, "Unit1", needReset: true, owner: owner, key: "ALARM_001");

            // Assert
            var alarm = service.Snapshot.First();
            Assert.Equal("报警内容", alarm.Content);
            Assert.Equal(AlarmLevel.H, alarm.Level);
            Assert.Equal("Unit1", alarm.Unit);
            Assert.True(alarm.NeedReset);
            Assert.Same(owner, alarm.Owner);
            Assert.Equal("ALARM_001", alarm.Key);
            Assert.Equal(1, alarm.Occurrence);
        }

        [Fact]
        public void AddAlarm_WithNeedReset_ShouldSetHasNeedResetAlarm()
        {
            // Arrange
            var service = new AlarmService();

            // Act
            service.AddAlarm("需要复位的报警", needReset: true);

            // Assert
            Assert.True(service.HasNeedResetAlarm);
        }

        [Fact]
        public void AddAlarm_WithoutNeedReset_ShouldNotSetHasNeedResetAlarm()
        {
            // Arrange
            var service = new AlarmService();

            // Act
            service.AddAlarm("不需要复位的报警", needReset: false);

            // Assert
            Assert.False(service.HasNeedResetAlarm);
        }

        #endregion

        #region 去重和更新测试

        [Fact]
        public void AddAlarm_DuplicateKey_ShouldUpdateExisting()
        {
            // Arrange
            var service = new AlarmService();
            service.AddAlarm("第一次报警", AlarmLevel.H, null, false, null, "DUP_KEY");

            // Act
            service.AddAlarm("更新后的报警", AlarmLevel.M, null, false, null, "DUP_KEY");

            // Assert
            Assert.Equal(1, service.AlarmCount); // 仍然只有一个报警
            var alarm = service.Snapshot.First();
            Assert.Equal("更新后的报警", alarm.Content);
            Assert.Equal(AlarmLevel.M, alarm.Level);
            Assert.Equal(2, alarm.Occurrence); // 发生次数递增
        }

        [Fact]
        public void AddAlarm_DuplicateKey_ShouldIncrementOccurrence()
        {
            // Arrange
            var service = new AlarmService();

            // Act
            for (int i = 0; i < 5; i++)
            {
                service.AddAlarm("重复报警", AlarmLevel.H, null, false, null, "REPEAT");
            }

            // Assert
            Assert.Equal(1, service.AlarmCount);
            Assert.Equal(5, service.Snapshot.First().Occurrence);
        }

        [Fact]
        public void AddAlarm_DifferentKeys_ShouldAddMultiple()
        {
            // Arrange
            var service = new AlarmService();

            // Act
            service.AddAlarm("报警1", AlarmLevel.H, null, false, null, "KEY_1");
            service.AddAlarm("报警2", AlarmLevel.H, null, false, null, "KEY_2");
            service.AddAlarm("报警3", AlarmLevel.H, null, false, null, "KEY_3");

            // Assert
            Assert.Equal(3, service.AlarmCount);
        }

        #endregion

        #region 删除测试

        [Fact]
        public void RemoveByKey_ExistingKey_ShouldRemoveAndReturnTrue()
        {
            // Arrange
            var service = new AlarmService();
            service.AddAlarm("要删除的报警", AlarmLevel.H, null, false, null, "TO_REMOVE");
            service.AddAlarm("保留的报警", AlarmLevel.H, null, false, null, "TO_KEEP");

            // Act
            var result = service.RemoveByKey("TO_REMOVE");

            // Assert
            Assert.True(result);
            Assert.Equal(1, service.AlarmCount);
            Assert.False(service.ExistsByKey("TO_REMOVE"));
            Assert.True(service.ExistsByKey("TO_KEEP"));
        }

        [Fact]
        public void RemoveByKey_NonExistingKey_ShouldReturnFalse()
        {
            // Arrange
            var service = new AlarmService();
            service.AddAlarm("报警", AlarmLevel.H, null, false, null, "EXISTING");

            // Act
            var result = service.RemoveByKey("NON_EXISTING");

            // Assert
            Assert.False(result);
            Assert.Equal(1, service.AlarmCount);
        }

        [Fact]
        public void RemoveByKey_NullOrEmpty_ShouldReturnFalse()
        {
            // Arrange
            var service = new AlarmService();
            service.AddAlarm("报警");

            // Act & Assert
            Assert.False(service.RemoveByKey(null!));
            Assert.False(service.RemoveByKey(""));
            Assert.False(service.RemoveByKey("   "));
            Assert.Equal(1, service.AlarmCount);
        }

        #endregion

        #region 清空测试

        [Fact]
        public void Clear_ShouldRemoveAllAlarms()
        {
            // Arrange
            var service = new AlarmService();
            service.AddAlarm("报警1");
            service.AddAlarm("报警2");
            service.AddAlarm("报警3");

            // Act
            service.Clear();

            // Assert
            Assert.Equal(0, service.AlarmCount);
            Assert.False(service.HasAlarm);
        }

        [Fact]
        public void Clear_EmptyService_ShouldNotThrow()
        {
            // Arrange
            var service = new AlarmService();

            // Act
            var ex = Record.Exception(() => service.Clear());

            // Assert
            Assert.Null(ex);
        }

        #endregion

        #region ExistsByKey 测试

        [Fact]
        public void ExistsByKey_ExistingKey_ShouldReturnTrue()
        {
            // Arrange
            var service = new AlarmService();
            service.AddAlarm("报警", AlarmLevel.H, null, false, null, "TEST_KEY");

            // Assert
            Assert.True(service.ExistsByKey("TEST_KEY"));
        }

        [Fact]
        public void ExistsByKey_NonExistingKey_ShouldReturnFalse()
        {
            // Arrange
            var service = new AlarmService();
            service.AddAlarm("报警", AlarmLevel.H, null, false, null, "EXISTING");

            // Assert
            Assert.False(service.ExistsByKey("NON_EXISTING"));
        }

        [Fact]
        public void ExistsByKey_NullOrWhitespace_ShouldReturnFalse()
        {
            // Arrange
            var service = new AlarmService();

            // Assert
            Assert.False(service.ExistsByKey(null!));
            Assert.False(service.ExistsByKey(""));
            Assert.False(service.ExistsByKey("   "));
        }

        #endregion

        #region Snapshot 测试

        [Fact]
        public void Snapshot_ShouldReturnCopy()
        {
            // Arrange
            var service = new AlarmService();
            service.AddAlarm("报警1");
            service.AddAlarm("报警2");

            // Act
            var snapshot1 = service.Snapshot;
            service.AddAlarm("报警3");
            var snapshot2 = service.Snapshot;

            // Assert
            Assert.Equal(2, snapshot1.Count);
            Assert.Equal(3, snapshot2.Count);
        }

        #endregion

        #region 事件测试

        [Fact]
        public void AddAlarm_ShouldRaiseAlarmChangedEvent_Added()
        {
            // Arrange
            var service = new AlarmService();
            AlarmChangedEventArgs? eventArgs = null;
            service.AlarmChanged += (_, e) => eventArgs = e;

            // Act
            service.AddAlarm("新报警", AlarmLevel.H, null, false, null, "NEW");

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(AlarmChangeKind.Added, eventArgs!.Kind);
            Assert.NotNull(eventArgs.Alarm);
            Assert.Equal("NEW", eventArgs.Alarm!.Key);
        }

        [Fact]
        public void AddAlarm_DuplicateKey_ShouldRaiseAlarmChangedEvent_Updated()
        {
            // Arrange
            var service = new AlarmService();
            service.AddAlarm("第一次", AlarmLevel.H, null, false, null, "DUP");

            AlarmChangedEventArgs? eventArgs = null;
            service.AlarmChanged += (_, e) => eventArgs = e;

            // Act
            service.AddAlarm("第二次", AlarmLevel.H, null, false, null, "DUP");

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(AlarmChangeKind.Updated, eventArgs!.Kind);
        }

        [Fact]
        public void RemoveByKey_ShouldRaiseAlarmChangedEvent_Removed()
        {
            // Arrange
            var service = new AlarmService();
            service.AddAlarm("报警", AlarmLevel.H, null, false, null, "TO_REMOVE");

            AlarmChangedEventArgs? eventArgs = null;
            service.AlarmChanged += (_, e) => eventArgs = e;

            // Act
            service.RemoveByKey("TO_REMOVE");

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(AlarmChangeKind.Removed, eventArgs!.Kind);
            Assert.Equal("TO_REMOVE", eventArgs.Alarm!.Key);
        }

        [Fact]
        public void Clear_ShouldRaiseAlarmChangedEvent_Cleared()
        {
            // Arrange
            var service = new AlarmService();
            service.AddAlarm("报警");

            AlarmChangedEventArgs? eventArgs = null;
            service.AlarmChanged += (_, e) => eventArgs = e;

            // Act
            service.Clear();

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal(AlarmChangeKind.Cleared, eventArgs!.Kind);
            Assert.Null(eventArgs.Alarm);
        }

        [Fact]
        public void AlarmListChanged_ShouldBeFiredOnAllChanges()
        {
            // Arrange
            var service = new AlarmService();
            var eventCount = 0;
            service.AlarmListChanged += (_, __) => eventCount++;

            // Act
            service.AddAlarm("报警1");
            service.AddAlarm("报警2");
            service.RemoveByKey("报警1");
            service.Clear();

            // Assert
            Assert.Equal(4, eventCount);
        }

        #endregion

        #region 线程安全测试

        [Fact]
        public async Task AlarmService_ShouldBeThreadSafe()
        {
            // Arrange
            var service = new AlarmService();
            var tasks = new List<Task>();
            var addCount = 100;
            var threadCount = 10;

            // Act
            for (int t = 0; t < threadCount; t++)
            {
                var threadId = t;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < addCount; i++)
                    {
                        service.AddAlarm($"报警_{threadId}_{i}", AlarmLevel.H, null, false, null, $"KEY_{threadId}_{i}");
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(addCount * threadCount, service.AlarmCount);
        }

        [Fact]
        public async Task AlarmService_ConcurrentAddAndRemove_ShouldNotThrow()
        {
            // Arrange
            var service = new AlarmService();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var exceptions = new List<Exception>();

            // Act
            var addTask = Task.Run(() =>
            {
                int i = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        service.AddAlarm($"报警_{i}", AlarmLevel.H, null, false, null, $"KEY_{i % 10}");
                        i++;
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            });

            var removeTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            service.RemoveByKey($"KEY_{i}");
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            });

            var readTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var _ = service.Snapshot;
                        var __ = service.AlarmCount;
                        var ___ = service.HasAlarm;
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
            });

            await Task.WhenAll(addTask, removeTask, readTask);

            // Assert
            Assert.Empty(exceptions);
        }

        #endregion

        #region Alarm 实体测试

        [Fact]
        public void Alarm_Create_ShouldSetDefaults()
        {
            // Act
            var alarm = Alarm.Create("测试内容");

            // Assert
            Assert.Equal("测试内容", alarm.Content);
            Assert.Equal("测试内容", alarm.Key);
            Assert.Equal(AlarmLevel.H, alarm.Level);
            Assert.False(alarm.NeedReset);
            Assert.Null(alarm.Owner);
            Assert.Null(alarm.Unit);
            Assert.Equal(1, alarm.Occurrence);
            Assert.True(alarm.AlarmTime <= DateTime.Now);
        }

        [Fact]
        public void Alarm_Create_WithCustomKey_ShouldUseCustomKey()
        {
            // Act
            var alarm = Alarm.Create("内容", AlarmLevel.L, key: "CUSTOM_KEY");

            // Assert
            Assert.Equal("CUSTOM_KEY", alarm.Key);
            Assert.Equal("内容", alarm.Content);
        }

        [Fact]
        public void Alarm_Create_EmptyKey_ShouldFallbackToContent()
        {
            // Act
            var alarm = Alarm.Create("内容", AlarmLevel.L, key: "");

            // Assert
            Assert.Equal("内容", alarm.Key);
        }

        #endregion

        #region AlarmLevel 测试

        [Theory]
        [InlineData(AlarmLevel.L)]
        [InlineData(AlarmLevel.M)]
        [InlineData(AlarmLevel.H)]
        public void AddAlarm_DifferentLevels_ShouldSetCorrectLevel(AlarmLevel level)
        {
            // Arrange
            var service = new AlarmService();

            // Act
            service.AddAlarm("报警", level, null, false, null, level.ToString());

            // Assert
            var alarm = service.Snapshot.First(a => a.Key == level.ToString());
            Assert.Equal(level, alarm.Level);
        }

        #endregion
    }
}
