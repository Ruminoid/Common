using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Ruminoid.Common.Utilities
{
    public class Progress : INotifyPropertyChanged
    {
        #region Data

        private double _percentage;

        public double Percentage
        {
            get => _percentage;
            set
            {
                _percentage = Math.Max(0, Math.Min(1, value));
                OnPropertyChanged();
            }
        }

        private string _title = "";

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        private string _description = "";

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        private string _detail = "";

        public string Detail
        {
            get => _detail;
            set
            {
                _detail = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Property Changed

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public interface IProgressable
    {
        Progress Progress { get; }
    }
}
