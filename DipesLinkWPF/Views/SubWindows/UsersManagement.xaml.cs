﻿using DipesLink.Models;
using DipesLink.Views.Extension;
using LiveChartsCore.VisualElements;
using RelationalDatabaseHelper.SQLite;
using SharedProgram.Shared;
using SQLite;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
namespace DipesLink.Views.SubWindows
{
    /// <summary>
    /// Interaction logic for UsersManagement.xaml
    /// </summary>
    /// 

    public partial class UsersManagement : Window
    {
        public static string OldPassword = string.Empty;

        bool isInit = true;
        SQLiteAsyncConnection db;

        public UsersManagement()
        {
            InitializeComponent();
            isInit = true;
            Loaded += UsersManagement_Loaded;
            InitDatabase();
        }
        void InitDatabase()
        {
            var databasePath = Path.Combine(SharedPaths.PathAccountsDb, "AccountDB.db");
            SQLiteConnectionString options = new(databasePath, true, key: "123456");
            db = new SQLiteAsyncConnection(options);
        }
        private void UsersManagement_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAllUser();
            isInit = false;
        }

        private async void LoadAllUser()
        {


            // Retrieve all records from the User table asynchronously
            var users = await db.Table<User>().ToListAsync();
            try
            {
                DataGridUsers.ItemsSource = users;
            }
            catch (Exception)
            {

                throw;
            }
        }
        public static List<UsersModel> ConvertToListOfStringArrays(List<IDictionary<string, object>> listDict)
        {
            List<UsersModel> resultList = new();

            foreach (var dict in listDict)
            {
                string[] array = new string[dict.Count];
                UsersModel user = new UsersModel();
                int i = 0;
                foreach (var pair in dict)
                {
                    i++;
                    if (pair.Key == "id")
                        user.Id = pair.Value.ToString();
                    if (pair.Key == "username")
                        user.Username = pair.Value.ToString();
                    if (pair.Key == "password")
                        user.Password = pair.Value.ToString();
                    if (pair.Key == "role")
                        user.Role = pair.Value.ToString();

                }
                resultList.Add(user);
            }

            return resultList;
        }

        private bool CheckInvalidInput()
        {
            // Regex for username
            //Must be 8-20 characters long.
            //Can include letters, numbers, and underscores.
            //Must start with a letter.

            //Regex for password
            //At least 8 characters long.
            //Must contain at least one uppercase letter.
            //Must contain at least one lowercase letter.
            //Must contain at least one digit.
            //Optionally, include special characters for stronger security.

            Regex usernameRegex = new Regex(@"^[a-zA-Z]\w{7,19}$");
            Regex passwordRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[A-Za-z\d@#$%^&+=]{8,}$");

            if (TextBoxUsername.Text != null)
            {
                bool isUsernameValid = usernameRegex.IsMatch(TextBoxUsername.Text);
                if (!isUsernameValid)
                {
                    CusMsgBox.Show("Username Invalid", "Input Validation", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Warning);
                    return false;
                }
            }
            if (TextBoxPassword.Text == null)
            {
                CusMsgBox.Show("Username Invalid", "Input Validation", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Warning);
                return false;
            }
            return true;

        }
        private async void CreateUser()
        {
            bool isCreated = false;
            
            try
            {
                User newUser = new User()
                {
                    username = TextBoxUsername.Text,
                    password = TextBoxPassword.Text,
                    role = ((ComboBoxItem)ComboBoxRole.SelectedItem).Content.ToString(),
                };
                var users = await db.Table<User>().ToListAsync();
                var user = users.FirstOrDefault(u => u.username == newUser.username);
                if (newUser.username != "" && newUser.password != "" && user == null )
                {
                    await db.RunInTransactionAsync(tran => {
                        // database calls inside the transaction
                        tran.Insert(newUser);
                    });

                    isCreated = true;
                    LoadAllUser();
                }
                else
                {
                    CusMsgBox.Show("Invalid User", "Create User", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Error);
                }

            }
            catch (Exception)
            {
                isCreated = false;
            }
            finally
            {
                if (isCreated)
                {
                    CusMsgBox.Show("Create User Done !", "Create User", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Info);
                }
            }
        }


        private async void EditUserPassword(string username, string newPassword, string oldPassword, string newRole, string confirmedPassword)
        {
            bool isChanged = false;

            try
            {
                // Update the user's password

                if (newPassword == confirmedPassword)
                {

                    User userToUpdate = await db.Table<User>().Where(u => u.username == username).FirstOrDefaultAsync();

                    if (userToUpdate != null)
                    {
                        userToUpdate.password = newPassword;
                        userToUpdate.role = newRole;
                        await db.RunInTransactionAsync(tran =>
                        {
                            tran.Update(userToUpdate);
                        });
                        LoadAllUser();
                        CusMsgBox.Show("Change user info successfully !", "Edit User", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Info);
                    }
                    else
                    {
                        CusMsgBox.Show("User not found for updating", "Edit User", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Error);
                    }
                }
                else
                {
                    CusMsgBox.Show("Unmatched Password", "Edit User", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Error);
                }


            }
            catch (Exception)
            {
                isChanged = false;
                CusMsgBox.Show("Change user info failed !", "Edit User", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Error);
            }
            finally
            {
                if (isChanged)
                {
                    CusMsgBox.Show($"Change info for {username} done !", "Edit User", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Info);
                }
            }

        }

        private bool CheckCurrentUser(string username)
        {
            string curUsername = Application.Current.Properties["Username"].ToString();
            return curUsername == username;
        }
        private async void DeleteUser(string username)
        {
            bool isDeleted = false;
            try
            {
                User userToDelete = await db.Table<User>().Where(u => u.username == username).FirstOrDefaultAsync();

                if (userToDelete != null)
                {
                    await db.RunInTransactionAsync(tran =>
                    {
                        tran.Delete(userToDelete);
                    });
                    isDeleted = true;
                    LoadAllUser();
                    CusMsgBox.Show("delete user successfully !", "Delete User", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Info);
                }
                else
                {
                    CusMsgBox.Show("User not found !", "Delete User", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Error);
                }

            }
            catch (Exception)
            {
                isDeleted = false;

                CusMsgBox.Show("Delete User Failed !", "Delete User", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Error);
            }
            finally
            {
                if (isDeleted)
                {
                    CusMsgBox.Show("Delete User Done !", "Delete User", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Info);
                }
            }
        }

        private void Red_Checked(object sender, RoutedEventArgs e)
        {

            if (isInit == true) { return; };
            var rad = sender as RadioButton;
            switch (rad.Name)
            {
                case "RadNew":
                    TextBoxPassword.IsEnabled = true;
                    ComboBoxRole.IsEnabled = true;
                    ToggleStackPanel("RadNew");
                    break;
                case "RadEdit":
                    TextBoxPassword.IsEnabled = true;
                    ComboBoxRole.IsEnabled = true;
                    ToggleStackPanel("RadEdit");
                    break;
                case "RadDel":
                    TextBoxPassword.IsEnabled = false;
                    ComboBoxRole.IsEnabled = false;
                    ToggleStackPanel("RadDel");
                    break;
                default:
                    break;
            }
        }
        public void ToggleStackPanel(string Rad)
        {
            if (Rad == "RadEdit")
            {
                MyStackPanel.Visibility = Visibility.Visible;
            }
            else
            {
                MyStackPanel.Visibility = Visibility.Collapsed;
            }
        }
        private async void SubmitClick(object sender, RoutedEventArgs e)
        {

            //ADD NEW
            if (RadNew.IsChecked == true)
            {
                if (CheckInvalidInput())
                {
                    var res = CusMsgBox.Show("Create new user ?", "Create new user", Enums.ViewEnums.ButtonStyleMessageBox.OKCancel, Enums.ViewEnums.ImageStyleMessageBox.Info);
                    if (res.Result)
                    {
                        CreateUser();
                        LoadAllUser();
                    }
                }
            }

            //EDIT
            if (RadEdit.IsChecked == true)
            {
                EditUserPassword(TextBoxUsername.Text, TextBoxPassword.Text, OldPassword, ((ComboBoxItem)ComboBoxRole.SelectedItem).Content.ToString(), ConfirmBoxPassword.Text);
            }
            //DELETE
            if (RadDel.IsChecked == true)
            {
                string inputUsername = TextBoxUsername.Text;
                if (!CheckCurrentUser(inputUsername))
                {
                    var res = CusMsgBox.Show($"Delete user: {inputUsername} ?", "Delete User", Enums.ViewEnums.ButtonStyleMessageBox.OKCancel, Enums.ViewEnums.ImageStyleMessageBox.Info);
                    if (res.Result)
                    {
                        DeleteUser(inputUsername);
                        LoadAllUser();
                    }
                }
                else
                {
                    CusMsgBox.Show("Can not delete current user!", "Delete User", Enums.ViewEnums.ButtonStyleMessageBox.OK, Enums.ViewEnums.ImageStyleMessageBox.Warning);
                }
            }
        }

        private void DataGridUsers_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            if (DataGridUsers.SelectedItem != null)
            {
                var item = DataGridUsers.SelectedItem; // This is the row data.
                                                       // Assuming the model has a property named 'Name'
                                                       //var username = item.GetType().GetProperty("Username").GetValue(item, null);
                var username = item.GetType().GetProperty("username").GetValue(item, null);

                TextBoxUsername.Text = username.ToString();
                var role = item.GetType().GetProperty("role").GetValue(item, null);
                if (role.ToString() == "Administrator")
                {
                    ComboBoxRole.SelectedIndex = 0;
                }
                else
                {
                    ComboBoxRole.SelectedIndex = 1;
                }
            }
        }

    }
}
