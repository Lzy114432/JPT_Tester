using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Ewan.Controls
{
    /// <summary>
    /// 工业级IO状态指示器控件
    /// 提供可视化的开关状态显示，支持自定义样式、动画效果和颜色主题
    /// </summary>
    public class EwanIO : Control
    {
        #region 静态构造函数

        /// <summary>
        /// 静态构造函数，设置默认样式
        /// </summary>
        static EwanIO()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(EwanIO), new FrameworkPropertyMetadata(typeof(EwanIO)));
        }

        #endregion

        #region 核心状态属性

        /// <summary>
        /// 获取或设置IO开关状态
        /// </summary>
        public bool IsOn
        {
            get => (bool)GetValue(IsOnProperty);
            set => SetValue(IsOnProperty, value);
        }

        /// <summary>
        /// IO开关状态依赖属性
        /// </summary>
        public static readonly DependencyProperty IsOnProperty =
            DependencyProperty.Register(nameof(IsOn), typeof(bool), typeof(EwanIO), 
                new PropertyMetadata(false, OnIsOnChanged));

        #endregion

        #region 尺寸和布局属性

        /// <summary>
        /// 获取或设置状态指示器的图标大小
        /// </summary>
        public double IconSize
        {
            get => (double)GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        /// <summary>
        /// 图标大小依赖属性
        /// </summary>
        public static readonly DependencyProperty IconSizeProperty =
            DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(EwanIO), 
                new PropertyMetadata(32.0, null, CoerceIconSize));

        #endregion

        #region 文本显示属性

        /// <summary>
        /// 获取或设置显示的标签文本
        /// </summary>
        public string LabelText
        {
            get => (string)GetValue(LabelTextProperty);
            set => SetValue(LabelTextProperty, value);
        }

        /// <summary>
        /// 标签文本依赖属性
        /// </summary>
        public static readonly DependencyProperty LabelTextProperty =
            DependencyProperty.Register(nameof(LabelText), typeof(string), typeof(EwanIO), 
                new PropertyMetadata("IO Status"));

        /// <summary>
        /// 获取或设置标签字体大小
        /// </summary>
        public double LabelFontSize
        {
            get => (double)GetValue(LabelFontSizeProperty);
            set => SetValue(LabelFontSizeProperty, value);
        }

        /// <summary>
        /// 标签字体大小依赖属性
        /// </summary>
        public static readonly DependencyProperty LabelFontSizeProperty =
            DependencyProperty.Register(nameof(LabelFontSize), typeof(double), typeof(EwanIO), 
                new PropertyMetadata(14.0, null, CoerceFontSize));

        /// <summary>
        /// 获取或设置标签字体粗细
        /// </summary>
        public FontWeight LabelFontWeight
        {
            get => (FontWeight)GetValue(LabelFontWeightProperty);
            set => SetValue(LabelFontWeightProperty, value);
        }

        /// <summary>
        /// 标签字体粗细依赖属性
        /// </summary>
        public static readonly DependencyProperty LabelFontWeightProperty =
            DependencyProperty.Register(nameof(LabelFontWeight), typeof(FontWeight), typeof(EwanIO), 
                new PropertyMetadata(FontWeights.Medium));

        /// <summary>
        /// 获取或设置标签前景色
        /// </summary>
        public Brush LabelForeground
        {
            get => (Brush)GetValue(LabelForegroundProperty);
            set => SetValue(LabelForegroundProperty, value);
        }

        /// <summary>
        /// 标签前景色依赖属性
        /// </summary>
        public static readonly DependencyProperty LabelForegroundProperty =
            DependencyProperty.Register(nameof(LabelForeground), typeof(Brush), typeof(EwanIO), 
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(70, 130, 180))));

        #endregion

        #region 颜色主题属性

        /// <summary>
        /// 获取或设置ON状态时的指示器颜色
        /// </summary>
        public Brush OnColor
        {
            get => (Brush)GetValue(OnColorProperty);
            set => SetValue(OnColorProperty, value);
        }

        /// <summary>
        /// ON状态颜色依赖属性
        /// </summary>
        public static readonly DependencyProperty OnColorProperty =
            DependencyProperty.Register(nameof(OnColor), typeof(Brush), typeof(EwanIO), 
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 255, 127))));

        /// <summary>
        /// 获取或设置OFF状态时的指示器颜色
        /// </summary>
        public Brush OffColor
        {
            get => (Brush)GetValue(OffColorProperty);
            set => SetValue(OffColorProperty, value);
        }

        /// <summary>
        /// OFF状态颜色依赖属性
        /// </summary>
        public static readonly DependencyProperty OffColorProperty =
            DependencyProperty.Register(nameof(OffColor), typeof(Brush), typeof(EwanIO), 
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(105, 105, 105))));

        /// <summary>
        /// 获取或设置指示器边框颜色
        /// </summary>
        public Brush IndicatorBorderBrush
        {
            get => (Brush)GetValue(IndicatorBorderBrushProperty);
            set => SetValue(IndicatorBorderBrushProperty, value);
        }

        /// <summary>
        /// 指示器边框颜色依赖属性
        /// </summary>
        public static readonly DependencyProperty IndicatorBorderBrushProperty =
            DependencyProperty.Register(nameof(IndicatorBorderBrush), typeof(Brush), typeof(EwanIO), 
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(70, 130, 180))));

        /// <summary>
        /// 获取或设置指示器边框厚度
        /// </summary>
        public double IndicatorBorderThickness
        {
            get => (double)GetValue(IndicatorBorderThicknessProperty);
            set => SetValue(IndicatorBorderThicknessProperty, value);
        }

        /// <summary>
        /// 指示器边框厚度依赖属性
        /// </summary>
        public static readonly DependencyProperty IndicatorBorderThicknessProperty =
            DependencyProperty.Register(nameof(IndicatorBorderThickness), typeof(double), typeof(EwanIO), 
                new PropertyMetadata(2.0, null, CoerceBorderThickness));

        #endregion

        #region 动画效果属性

        /// <summary>
        /// 获取或设置是否启用状态切换动画
        /// </summary>
        public bool EnableAnimation
        {
            get => (bool)GetValue(EnableAnimationProperty);
            set => SetValue(EnableAnimationProperty, value);
        }

        /// <summary>
        /// 动画启用依赖属性
        /// </summary>
        public static readonly DependencyProperty EnableAnimationProperty =
            DependencyProperty.Register(nameof(EnableAnimation), typeof(bool), typeof(EwanIO), 
                new PropertyMetadata(true));

        /// <summary>
        /// 获取或设置动画持续时间（毫秒）
        /// </summary>
        public double AnimationDuration
        {
            get => (double)GetValue(AnimationDurationProperty);
            set => SetValue(AnimationDurationProperty, value);
        }

        /// <summary>
        /// 动画持续时间依赖属性
        /// </summary>
        public static readonly DependencyProperty AnimationDurationProperty =
            DependencyProperty.Register(nameof(AnimationDuration), typeof(double), typeof(EwanIO), 
                new PropertyMetadata(300.0, null, CoerceAnimationDuration));

        /// <summary>
        /// 获取或设置是否在ON状态时显示发光效果
        /// </summary>
        public bool ShowGlowEffect
        {
            get => (bool)GetValue(ShowGlowEffectProperty);
            set => SetValue(ShowGlowEffectProperty, value);
        }

        /// <summary>
        /// 发光效果依赖属性
        /// </summary>
        public static readonly DependencyProperty ShowGlowEffectProperty =
            DependencyProperty.Register(nameof(ShowGlowEffect), typeof(bool), typeof(EwanIO), 
                new PropertyMetadata(true));

        #endregion

        #region 属性值约束回调

        /// <summary>
        /// 约束图标大小在合理范围内
        /// </summary>
        private static object CoerceIconSize(DependencyObject d, object baseValue)
        {
            double value = (double)baseValue;
            return Math.Max(8.0, Math.Min(200.0, value));
        }

        /// <summary>
        /// 约束字体大小在合理范围内
        /// </summary>
        private static object CoerceFontSize(DependencyObject d, object baseValue)
        {
            double value = (double)baseValue;
            return Math.Max(6.0, Math.Min(72.0, value));
        }

        /// <summary>
        /// 约束边框厚度在合理范围内
        /// </summary>
        private static object CoerceBorderThickness(DependencyObject d, object baseValue)
        {
            double value = (double)baseValue;
            return Math.Max(0.0, Math.Min(10.0, value));
        }

        /// <summary>
        /// 约束动画持续时间在合理范围内
        /// </summary>
        private static object CoerceAnimationDuration(DependencyObject d, object baseValue)
        {
            double value = (double)baseValue;
            return Math.Max(50.0, Math.Min(2000.0, value));
        }

        #endregion

        #region 属性更改事件处理

        /// <summary>
        /// IsOn属性更改时的处理
        /// </summary>
        private static void OnIsOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EwanIO control)
            {
                control.OnIsOnChanged((bool)e.OldValue, (bool)e.NewValue);
            }
        }

        /// <summary>
        /// 处理状态更改
        /// </summary>
        protected virtual void OnIsOnChanged(bool oldValue, bool newValue)
        {
            // 可以在此处添加自定义状态更改逻辑
            // 例如触发自定义事件或执行额外的动画
        }

        #endregion
    }
}
