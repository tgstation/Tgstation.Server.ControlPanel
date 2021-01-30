using System;
using ReactiveUI;

namespace Tgstation.Server.ControlPanel.ViewModels
{
	public class ViewModelBase : ReactiveObject
	{
		public bool IsSelected
		{
			get => isSelected;
			set
			{
				var wasClick = value && !isSelected;
				isSelected = value;
				if (wasClick)
					TryClick();
			}
		}

		bool isSelected;

		async void TryClick()
		{
			if (this is ITreeNode treeNode)
				try
				{
					await treeNode.HandleClick(default);
				}
				catch (Exception ex)
				{
					MainWindowViewModel.HandleException(ex);
				}
		}
	}
}
