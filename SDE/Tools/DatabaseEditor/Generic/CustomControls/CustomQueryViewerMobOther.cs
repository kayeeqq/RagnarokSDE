﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Database;
using Database.Commands;
using ErrorManager;
using SDE.Tools.DatabaseEditor.Engines;
using SDE.Tools.DatabaseEditor.Generic.Lists;
using SDE.Tools.DatabaseEditor.Generic.TabsMakerCore;
using SDE.Tools.DatabaseEditor.WPF;
using TokeiLibrary;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles.ListView;
using Extensions = SDE.Others.Extensions;

namespace SDE.Tools.DatabaseEditor.Generic.CustomControls {
	public class CustomQueryViewerMobOther<TKey, TValue> : ICustomControl<TKey, TValue> where TValue : Tuple {
		private readonly int _rSpan;
		private readonly int _row;
		private RangeListView _lv;
		private GDbTabWrapper<TKey, TValue> _tab;
		private Action<TValue> _updateAction;

		public CustomQueryViewerMobOther(int row, int rSpan) {
			_row = row;
			_rSpan = rSpan;
		}

		#region ICustomProperty<TKey,TValue> Members

		public void Init(GDbTabWrapper<TKey, TValue> tab, DisplayableProperty<TKey, TValue> dp) {
			_tab = tab;
			Grid grid = new Grid();

			grid.SetValue(Grid.RowProperty, _row);
			grid.SetValue(Grid.RowSpanProperty, _rSpan);
			grid.SetValue(Grid.ColumnProperty, 0);
			grid.SetValue(Grid.ColumnSpanProperty, 5);

			grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(-1, GridUnitType.Auto)});
			grid.RowDefinitions.Add(new RowDefinition());

			grid.ColumnDefinitions.Add(new ColumnDefinition());
			grid.ColumnDefinitions.Add(new ColumnDefinition());
			grid.ColumnDefinitions.Add(new ColumnDefinition());

			Label label = new Label();
			label.Content = "Normal drops";
			label.FontStyle = FontStyles.Italic;
			label.Padding = new Thickness(0);
			label.Margin = new Thickness(3);

			_lv = new RangeListView();
			_lv.SetValue(TextSearch.TextPathProperty, "ID");
			_lv.SetValue(WpfUtils.IsGridSortableProperty, true);
			_lv.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
			_lv.SetValue(Grid.RowProperty, _row);
			_lv.FocusVisualStyle = null;
			_lv.Margin = new Thickness(3);
			_lv.BorderThickness = new Thickness(1);
			_lv.PreviewMouseRightButtonUp += _lv_PreviewMouseRightButtonUp;

			//Extensions.Gen1(_lv);

			//_lv.BorderThickness = new Thickness(0);
			Extensions.GenerateListViewTemplate(_lv, new ListViewDataTemplateHelper.GeneralColumnInfo[] {
					new ListViewDataTemplateHelper.GeneralColumnInfo {Header = ServerItemAttributes.Id.DisplayName, DisplayExpression = "ID", SearchGetAccessor = "ID", FixedWidth = 45, TextAlignment = TextAlignment.Right, ToolTipBinding = "ID"},
					new ListViewDataTemplateHelper.RangeColumnInfo {Header = ServerItemAttributes.Name.DisplayName, DisplayExpression = "Name", SearchGetAccessor = "Name", IsFill = true, ToolTipBinding = "Name", TextWrapping = TextWrapping.Wrap, MinWidth = 40 },
					new ListViewDataTemplateHelper.GeneralColumnInfo {Header = "Drop %", DisplayExpression = "Drop", SearchGetAccessor = "DropOriginal", ToolTipBinding = "DropOriginal", FixedWidth = 60, TextAlignment = TextAlignment.Right},
				}, new DefaultListViewComparer<MobDropView>(), new string[] { "Deleted", "Red", "Added", "Blue", "Default", "Black", "IsCard", "Green" });

			//Extensions.Gen2(_lv);

			_lv.ContextMenu = new ContextMenu();
			_lv.MouseDoubleClick += new MouseButtonEventHandler(_lv_MouseDoubleClick);

			MenuItem miSelect = new MenuItem { Header = "Select", Icon = new Image { Source = (BitmapSource) ApplicationManager.PreloadResourceImage("arrowdown.png") } };
			MenuItem miEditDrop = new MenuItem { Header = "Edit", Icon = new Image { Source = (BitmapSource) ApplicationManager.PreloadResourceImage("properties.png") } };
			MenuItem miRemoveDrop = new MenuItem { Header = "Remove", Icon = new Image { Source = (BitmapSource) ApplicationManager.PreloadResourceImage("delete.png") } };
			MenuItem miAddDrop = new MenuItem { Header = "Add normal drop", Icon = new Image { Source = (BitmapSource) ApplicationManager.PreloadResourceImage("add.png") } };
			MenuItem miAddCardDrop = new MenuItem { Header = "Add card", Icon = new Image { Source = (BitmapSource) ApplicationManager.PreloadResourceImage("add.png") } };

			_lv.ContextMenu.Items.Add(miSelect);
			_lv.ContextMenu.Items.Add(miEditDrop);
			_lv.ContextMenu.Items.Add(miRemoveDrop);
			_lv.ContextMenu.Items.Add(miAddDrop);
			_lv.ContextMenu.Items.Add(miAddCardDrop);

			miSelect.Click += new RoutedEventHandler(_miSelect_Click);
			miEditDrop.Click += new RoutedEventHandler(_miEditDrop_Click);
			miRemoveDrop.Click += new RoutedEventHandler(_miRemoveDrop_Click);
			miAddDrop.Click += new RoutedEventHandler(_miAddDrop_Click);
			miAddCardDrop.Click += new RoutedEventHandler(_miAddCardDrop_Click);

			_updateAction = new Action<TValue>(delegate(TValue item) {
				List<MobDropView> result = new List<MobDropView>();
				Table<int, ReadableTuple<int>> btable = ((GenericDatabase)tab.Database).GetTable<int>(ServerDBs.Items);

				try {
					int startIndex = ServerMobAttributes.Drop1ID.Index;

					for (int j = 0; j < 20; j += 2) {
						string value = (string)item.GetRawValue(startIndex + j);

						if (string.IsNullOrEmpty(value) || value == "0")
							continue;

						result.Add(new MobDropView((ReadableTuple<int>)(object)item, startIndex + j, btable));
					}
				}
				catch { }

				_lv.ItemsSource = new RangeObservableCollection<MobDropView>(result.OrderBy(p => p, Extensions.BindDefaultSearch<MobDropView>(_lv, "ID")));
			});

			//dp.AddResetAction(() => _lv.Items.OfType<MobDropView>().ToList().ForEach(p => p.RemoveEvents()));
			dp.AddUpdateAction(_updateAction);
			dp.AddResetField(_lv);

			grid.Children.Add(label);
			grid.Children.Add(_lv);
			tab.PropertiesGrid.Children.Add(grid);
		}

		#endregion

		private void _miAddCardDrop_Click(object sender, RoutedEventArgs e) {
			TValue item = (TValue)_tab.List.SelectedItem;

			if (item.GetValue<int>(ServerMobAttributes.DropCardid) != 0) {
				_lv.SelectedItem = _lv.Items.OfType<MobDropView>().FirstOrDefault(p => p.IsCard);
				_miEditDrop_Click(sender, e);
				return;
			}

			Table<int, ReadableTuple<int>> btable = ((GenericDatabase)_tab.Database).GetTable<int>(ServerDBs.Mobs);

			try {
				DropEdit dialog = new DropEdit("", "", _tab.GetDb<int>(ServerDBs.Items));
				dialog.Owner = WpfUtilities.TopWindow;

				if (dialog.ShowDialog() == true) {
					string sid = dialog.Id;
					string svalue = dialog.DropChance;
					int value;
					int id;

					Int32.TryParse(sid, out id);

					if (id <= 0)
						return;

					if (!Extensions.GetValue(svalue, out value)) {
						ErrorHandler.HandleException("Invalid format (integer or float value only)");
						return;
					}

					try {
						btable.Commands.BeginEdit(new GroupCommand<TKey, TValue>());

						int startIndex = ServerMobAttributes.DropCardid.Index;

						for (int i = 0; i < 2; i += 2) {
							if (item.GetValue<int>(startIndex + i) == 0) {
								btable.Commands.StoreAndExecute(new ChangeTupleProperty<int, ReadableTuple<int>>((ReadableTuple<int>)(object)item, ServerMobAttributes.AttributeList.Attributes[startIndex + i], id));
								btable.Commands.StoreAndExecute(new ChangeTupleProperty<int, ReadableTuple<int>>((ReadableTuple<int>)(object)item, ServerMobAttributes.AttributeList.Attributes[startIndex + i + 1], value));
								break;
							}
						}

					}
					finally {
						btable.Commands.EndEdit();
					}

					//_lv.Items.OfType<MobDropView>().ToList().ForEach(p => p.RemoveEvents());
					_lv.ItemsSource = null;
					_updateAction(item);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _miAddDrop_Click(object sender, RoutedEventArgs e) {
			if (_lv.Items.Count >= 10) {
				ErrorHandler.HandleException("You cannot add more than 10 drops. Delete an item and then add a new one.");
				return;
			}

			Table<int, ReadableTuple<int>> btable = ((GenericDatabase)_tab.Database).GetTable<int>(ServerDBs.Mobs);

			try {
				DropEdit dialog = new DropEdit("", "", _tab.GetDb<int>(ServerDBs.Items));
				dialog.Owner = WpfUtilities.TopWindow;

				if (dialog.ShowDialog() == true) {
					string sid = dialog.Id;
					string svalue = dialog.DropChance;
					int value;
					int id;

					Int32.TryParse(sid, out id);

					if (id <= 0)
						return;

					if (!Extensions.GetValue(svalue, out value)) {
						ErrorHandler.HandleException("Invalid format (integer or float value only)");
						return;
					}

					TValue item = (TValue)_tab.List.SelectedItem;

					try {
						btable.Commands.BeginEdit(new GroupCommand<TKey, TValue>());

						int startIndex = ServerMobAttributes.Drop1ID.Index;

						for (int i = 0; i < 18; i += 2) {
							if (item.GetValue<int>(startIndex + i) == 0) {
								btable.Commands.StoreAndExecute(new ChangeTupleProperty<int, ReadableTuple<int>>((ReadableTuple<int>)(object)item, ServerMobAttributes.AttributeList.Attributes[startIndex + i], id));
								btable.Commands.StoreAndExecute(new ChangeTupleProperty<int, ReadableTuple<int>>((ReadableTuple<int>)(object)item, ServerMobAttributes.AttributeList.Attributes[startIndex + i + 1], value));
								break;
							}
						}

					}
					finally {
						btable.Commands.EndEdit();
					}

					//_lv.Items.OfType<MobDropView>().ToList().ForEach(p => p.RemoveEvents());
					_lv.ItemsSource = null;
					_updateAction(item);
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _miEditDrop_Click(object sender, RoutedEventArgs e) {
			if (_lv.SelectedItems.Count <= 0)
				return;

			Table<int, ReadableTuple<int>> btable = ((GenericDatabase)_tab.Database).GetTable<int>(ServerDBs.Mobs);

			try {
				var selectedItem = (MobDropView) _lv.SelectedItem;
				DropEdit dialog = new DropEdit(selectedItem.ID.ToString(CultureInfo.InvariantCulture), selectedItem.DropOriginal.ToString(CultureInfo.InvariantCulture), _tab.GetDb<int>(ServerDBs.Items));
				dialog.Owner = WpfUtilities.TopWindow;

				if (dialog.ShowDialog() == true) {
					string sid = dialog.Id;
					string svalue = dialog.DropChance;
					int value;
					int id;

					Int32.TryParse(sid, out id);

					if (!Extensions.GetValue(svalue, out value)) {
						ErrorHandler.HandleException("Invalid format (integer or float value only)");
						return;
					}

					if (id <= 0) {
						return;
					}

					try {
						btable.Commands.BeginEdit(new GroupCommand<TKey, TValue>());

						btable.Commands.StoreAndExecute(new ChangeTupleProperty<int, ReadableTuple<int>>(selectedItem.Tuple, ServerMobAttributes.AttributeList.Attributes[selectedItem.AttributeIndex], id));
						btable.Commands.StoreAndExecute(new ChangeTupleProperty<int, ReadableTuple<int>>(selectedItem.Tuple, ServerMobAttributes.AttributeList.Attributes[selectedItem.AttributeIndex + 1], value));
					}
					finally {
						btable.Commands.EndEdit();
					}

					selectedItem.Update();
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}

			btable.Commands.EndEdit();
		}

		private void _miRemoveDrop_Click(object sender, RoutedEventArgs e) {
			if (_lv.SelectedItems.Count <= 0)
				return;

			Table<int, ReadableTuple<int>> btable = ((GenericDatabase)_tab.Database).GetTable<int>(ServerDBs.Mobs);

			btable.Commands.BeginEdit(new GroupCommand<int, ReadableTuple<int>>());

			try {
				for (int i = 0; i < _lv.SelectedItems.Count; i++) {
					var selectedItem = (MobDropView) _lv.SelectedItems[i];
					var p = (ReadableTuple<int>)_tab.List.SelectedItem;

					btable.Commands.StoreAndExecute(new ChangeTupleProperty<int, ReadableTuple<int>>(p, ServerMobAttributes.AttributeList.Attributes[selectedItem.AttributeIndex], 0));
					btable.Commands.StoreAndExecute(new ChangeTupleProperty<int, ReadableTuple<int>>(p, ServerMobAttributes.AttributeList.Attributes[selectedItem.AttributeIndex + 1], 0));

					((RangeObservableCollection<MobDropView>) _lv.ItemsSource).Remove(selectedItem);
					i--;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}

			btable.Commands.EndEdit();
		}

		private void _lv_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			_miEditDrop_Click(sender, null);
		}

		private void _lv_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
			try {
				object item = _lv.InputHitTest(e.GetPosition(_lv));

				if (item is ScrollViewer) {
					((MenuItem)_lv.ContextMenu.Items[0]).Visibility = Visibility.Collapsed;
					((MenuItem)_lv.ContextMenu.Items[1]).Visibility = Visibility.Collapsed;
					((MenuItem)_lv.ContextMenu.Items[2]).Visibility = Visibility.Collapsed;
				}
				else {
					((MenuItem)_lv.ContextMenu.Items[0]).Visibility = Visibility.Visible;
					((MenuItem)_lv.ContextMenu.Items[1]).Visibility = Visibility.Visible;
					((MenuItem)_lv.ContextMenu.Items[2]).Visibility = Visibility.Visible;
				}
			}
			catch (Exception err) {
				ErrorHandler.HandleException(err);
			}
		}

		private void _miSelect_Click(object sender, RoutedEventArgs e) {
			if (_lv.SelectedItems.Count > 0) {
				TabNavigationEngine.SelectList(ServerDBs.Items, _lv.SelectedItems.Cast<MobDropView>().Select(p => p.ID));
			}
		}

		#region Nested type: MobDropView

		public class MobDropView :  INotifyPropertyChanged {
			private readonly int _index;
			private readonly Table<int, ReadableTuple<int>> _itemsTable;
			private readonly ReadableTuple<int> _tuple;

			public MobDropView(ReadableTuple<int> tuple, int index, Table<int, ReadableTuple<int>> itemsTable) {
				_tuple = tuple;
				_index = index;
				_itemsTable = itemsTable;
				_tuple.TupleModified += _tuple_TupleModified;

				_reload();
			}

			public int AttributeIndex {
				get { return _index; }
			}

			public ReadableTuple<int> Tuple {
				get { return _tuple; }
			}

			public string Drop { get; private set; }

			public int ID { get; private set; }
			public string Name { get; private set; }
			public string MVP { get; private set; }
			public int DropOriginal { get; private set; }

			public bool Default {
				get { return true; }
			}

			public bool IsCard {
				get { return AttributeIndex == ServerMobAttributes.DropCardid.Index; }
			}

			#region INotifyPropertyChanged Members

			public event PropertyChangedEventHandler PropertyChanged;

			#endregion

			private void _tuple_TupleModified(object sender, bool value) {
				OnPropertyChanged();
			}

			private void _reload() {
				ID = _tuple.GetValue<int>(_index);

				Name = "";

				if (_itemsTable.ContainsKey(ID)) {
					Name = (string)_itemsTable.GetTuple(ID).GetRawValue(ServerItemAttributes.Name.Index);
				}

				DropOriginal = Int32.Parse(((string)_tuple.GetRawValue(_index + 1)));
				Drop = String.Format("{0:0.00} %", DropOriginal / 100f);
				MVP = _index < ServerMobAttributes.Drop1ID.Index ? "MVP" : "";
			}

			protected virtual void OnPropertyChanged() {
				_reload();

				PropertyChangedEventHandler handler = PropertyChanged;
				if (handler != null) handler(this, new PropertyChangedEventArgs(""));
			}

			public void Update() {
				_reload();
			}

			//public void RemoveEvents() {
			//    if (_tuple != null) {
			//        _tuple.TupleModified -= _tuple_TupleModified;
			//    }
			//}
		}

		#endregion
	}
}
