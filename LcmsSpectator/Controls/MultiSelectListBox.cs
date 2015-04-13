﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MultiSelectListBox.cs" company="Pacific Northwest National Laboratory">
//   2015 Pacific Northwest National Laboratory
// </copyright>
// <author>Christopher Wilkins</author>
// <summary>
//   ListBox that exposes the SelectedItems as a dependency property for use with MultiSelection.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LcmsSpectator.Controls
{
    using System.Collections;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// ListBox that exposes the SelectedItems as a dependency property for use with MultiSelection.
    /// </summary>
    public class MultiSelectListBox : ListBox
    {
        /// <summary>
        /// A value indicating whether the change to the selected items in the list was caused by an internal update.
        /// </summary>
        private bool internalChange;

        /// <summary>
        /// Initializes static members of the <see cref="MultiSelectListBox"/> class.
        /// </summary>
        static MultiSelectListBox()
        {
            SelectedItemsSourceProperty = DependencyProperty.Register(
                "SelectedItemsSource",
                typeof(IList),
                typeof(MultiSelectListBox),
                new FrameworkPropertyMetadata(OnSelectedItemsSourceChanged));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiSelectListBox"/> class.
        /// </summary>
        public MultiSelectListBox()
        {
            this.internalChange = false;
            this.SelectionMode = SelectionMode.Extended;
            this.SelectedItemsSource = SelectedItems.OfType<object>().ToList();
            this.SelectionChanged += this.SelectedItemsChanged;
        }

        /// <summary>
        /// Gets the dependency property that exposes the selected items list for view model binding.
        /// </summary>
        public static DependencyProperty SelectedItemsSourceProperty { get; private set; }

        /// <summary>
        /// Gets or sets the source list for the selected items for this ListBox.
        /// </summary>
        public IList SelectedItemsSource
        {
            get
            {
                return (IList)this.GetValue(SelectedItemsSourceProperty);
            }

            set
            {
                this.SetCurrentValue(SelectedItemsSourceProperty, value);
            }
        }

        /// <summary>
        /// Event handler that is triggered when the SelectedItemsSource dependency property changes.
        /// </summary>
        /// <param name="d">The dependency object (the sender).</param>
        /// <param name="e">The event arguments.</param>
        private static void OnSelectedItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var listBox = (MultiSelectListBox)d;
            if (listBox.SelectedItemsSource == null)
            {
                return;
            }

            listBox.internalChange = true;
            listBox.SelectedItems.Clear();
            foreach (var item in listBox.SelectedItemsSource)
            {
                listBox.SelectedItems.Add(item);
            }

            listBox.internalChange = false;
        }

        /// <summary>
        /// Event handler that is triggered when items are selected in the ListBox.
        /// </summary>
        /// <param name="sender">The sender DataGrid.</param>
        /// <param name="e">The event arguments.</param>
        private void SelectedItemsChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as MultiSelectListBox;
            if (listBox == null)
            {
                return;
            }

            if (!listBox.internalChange)
            {
                this.SelectedItemsSource = listBox.SelectedItems.OfType<object>().ToList();
            }
        }
    }
}
