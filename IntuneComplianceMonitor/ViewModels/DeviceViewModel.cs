using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace IntuneComplianceMonitor.ViewModels
{
    public class DeviceViewModel : INotifyPropertyChanged
    {
        private string _deviceId;
        private string _deviceName;
        private string _owner;
        private string _deviceType;
        private DateTime _lastCheckIn;
        private string _ownership;
        private string _osVersion;
        private string _serialNumber;
        private string _manufacturer;
        private string _model;
        private List<string> _complianceIssues;

        public DeviceViewModel()
        {
            ComplianceIssues = new List<string>();
        }

        public string DeviceId
        {
            get => _deviceId;
            set
            {
                if (_deviceId != value)
                {
                    _deviceId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DeviceName
        {
            get => _deviceName;
            set
            {
                if (_deviceName != value)
                {
                    _deviceName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Owner
        {
            get => _owner;
            set
            {
                if (_owner != value)
                {
                    _owner = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DeviceType
        {
            get => _deviceType;
            set
            {
                if (_deviceType != value)
                {
                    _deviceType = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime LastCheckIn
        {
            get => _lastCheckIn;
            set
            {
                if (_lastCheckIn != value)
                {
                    _lastCheckIn = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LastCheckInDisplay));
                    OnPropertyChanged(nameof(TimeSinceLastCheckIn));
                    OnPropertyChanged(nameof(TimeSinceLastCheckInDisplay));
                }
            }
        }

        public string Ownership
        {
            get => _ownership;
            set
            {
                if (_ownership != value)
                {
                    _ownership = value;
                    OnPropertyChanged();
                }
            }
        }

        public string OSVersion
        {
            get => _osVersion;
            set
            {
                if (_osVersion != value)
                {
                    _osVersion = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (_serialNumber != value)
                {
                    _serialNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Manufacturer
        {
            get => _manufacturer;
            set
            {
                if (_manufacturer != value)
                {
                    _manufacturer = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Model
        {
            get => _model;
            set
            {
                if (_model != value)
                {
                    _model = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<string> ComplianceIssues
        {
            get => _complianceIssues;
            set
            {
                if (_complianceIssues != value)
                {
                    _complianceIssues = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCompliant));
                    OnPropertyChanged(nameof(ComplianceStatus));
                    OnPropertyChanged(nameof(ComplianceIssuesList));
                }
            }
        }

        public string LastCheckInDisplay => LastCheckIn.ToString("g");
        public bool IsCompliant => !ComplianceIssues.Any();
        public string ComplianceStatus => IsCompliant ? "Compliant" : "Non-Compliant";
        public string ComplianceIssuesList => string.Join(", ", ComplianceIssues);
        public TimeSpan TimeSinceLastCheckIn => DateTime.Now - LastCheckIn;

        public string TimeSinceLastCheckInDisplay
        {
            get
            {
                var span = TimeSinceLastCheckIn;
                if (span.TotalDays > 1)
                    return $"{(int)span.TotalDays} days ago";
                if (span.TotalHours > 1)
                    return $"{(int)span.TotalHours} hours ago";
                return $"{(int)span.TotalMinutes} minutes ago";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}