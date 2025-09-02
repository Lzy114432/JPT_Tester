using Prism.Mvvm;

namespace MarkingMachineFeeder.Model
{
    public class IOMapping : BindableBase
    {
        private int _logicalIndex;
        private string _name;
        private int _physicalIndex;
        private bool _isNormallyOpen;

        public int LogicalIndex
        {
            get => _logicalIndex;
            set => SetProperty(ref _logicalIndex, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int PhysicalIndex
        {
            get => _physicalIndex;
            set => SetProperty(ref _physicalIndex, value);
        }

        public bool IsNormallyOpen
        {
            get => _isNormallyOpen;
            set => SetProperty(ref _isNormallyOpen, value);
        }
    }

    public class StatusOption
    {
        public string Display { get; set; }
        public bool Value { get; set; }
    }
}