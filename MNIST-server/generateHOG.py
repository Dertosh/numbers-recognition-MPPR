
import os
import sys
import urllib.request
from sklearn.externals import joblib
from sklearn import datasets
from skimage.feature import hog
from sklearn.svm import LinearSVC
import numpy as np
from collections import Counter

#игнорирование предупреждений
import warnings
warnings.simplefilter("ignore", DeprecationWarning)

#изменение текущего окружения на окружение скрипта
pathProgramm = os.path.dirname(sys.argv[0])
os.chdir(pathProgramm)

file_path = "scikit_learn_data/mldata/"

if not os.path.exists(file_path + 'mnist-original.mat'):

    directory = os.path.dirname(file_path)

    try:
        os.stat(directory)
    except IOError as err:
        os.makedirs(directory)

    print('Beginning mnist-original.mat download ...')

    url = 'https://raw.githubusercontent.com/amplab/datascience-sp14/master/lab7/mldata/mnist-original.mat'
    urllib.request.urlretrieve(url, file_path + 'mnist-original.mat')
    if not os.path.exists(file_path + 'mnist-original.mat'):
        exit()

# Загрузка dataset с mldata.org
print('Loading MNIST Original...')
mnist = datasets.fetch_mldata('MNIST Original', data_home='scikit_learn_data')

# Получение массива элементов и меток
numbers = np.array(mnist.data, 'int16')
targets = np.array(mnist.target, 'int')

# HOG-массив python
hog_array = []

# Для HOG задаем ячейки, равные 14x14
# то для каждого NMIST-числа будем иметь 4 ячейки на один блок.
# Размер вектора направления = 9
# Размер HOG вектора равен 9x4=36
# http://bit.ly/2nBnUqS
print(Counter(targets))

for number in numbers:
    # pixels_per_cell - количество пикселей на ячейку
    # cells_per_block - количество ячеек на блок
    # orientations - количество каналов
    fd = hog(number.reshape((28, 28)), 
                orientations=9, pixels_per_cell=(14, 14), 
             cells_per_block=(1, 1), visualize=False, block_norm='L2-Hys')
    hog_array.append(fd)

hog_numbers = np.array(hog_array, 'float64')

print(Counter(targets))

# Создание SVM объектов (метод опорных векторов)
# http://bit.ly/2B36mJR
clf = LinearSVC()

# Представление в виде модели
clf.fit(hog_numbers, targets)

# Сохраненеи дампа
joblib.dump(clf, "digits_cls.pkl", compress=3)
