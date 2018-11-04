
import sys
import os
from time import sleep
import cv2
from sklearn.externals import joblib
from skimage.feature import hog
import numpy as np

import time
import zmq

from PIL import Image, ImageFilter

# Запуск сервера
context = zmq.Context(1)
server = context.socket(zmq.REP)
server.bind("tcp://*:5556")

# Загрузка HOG-классификатора
clf = joblib.load("digits_cls.pkl")

print("I: Server started")

pathProgramm = os.path.dirname(sys.argv[0])
os.chdir(pathProgramm)

while True:
    request = server.recv()

    try:
        image=Image.open("image.png")  # Открытие изображения
        imageconvert = image.convert('L')
    except IOError:
        server.send(request)
        print("E: No image")
        continue
        

    width = int(round(float(imageconvert.size[0]+300)))
    height = int(round(float(imageconvert.size[1]+300)))

    newImage = Image.new('L', (width, height), (255))  # Новое полотно на 50 больше

    newImage.paste(imageconvert, (150,150))
    newImage.save("image.png")
  
    # Чтение фотографии
    im = cv2.imread("image.png")

    # Конвертаця в серый слой и применение фильтра Гаусса 
    im_gray = cv2.cvtColor(im, cv2.COLOR_BGR2GRAY)
    im_gray = cv2.GaussianBlur(im_gray, (5, 5), 0)

    # Поиск изображения
    ret, im_th = cv2.threshold(im_gray, 90, 255, cv2.THRESH_BINARY_INV)

    # Поиск контуров на изображении
    # im_th - исходное изображение
    # RETR_EXTERNAL - режим поиска контуров
    # CHAIN_APPROX_SIMPLE - метод приближения контуров
    _, ctrs, hier = cv2.findContours(im_th.copy(), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    # Окружить прямоугольниками каждый контур
    #rects = [cv2.boundingRect(ctr) for ctr in ctrs]
    if not ctrs:
        continue
    
    try:
        # Для каждой обнаруженной области, найти  HOG и 
        # предсказать по полученной модели Linear SVM цифру
        rect = cv2.boundingRect(ctrs[0])
        # Нарисовать прямоугольник
        cv2.rectangle(im, (rect[0], rect[1]), (rect[0] + rect[2], rect[1] + rect[3]), (0, 255, 0), 3) 
        # Вычленить полученный прямоугольник из изображения
        leng = int(rect[3] * 1.6)
        pt1 = int(rect[1] + rect[3] // 2 - leng // 2)
        pt2 = int(rect[0] + rect[2] // 2 - leng // 2)
        roi = im_th[pt1:pt1+leng, pt2:pt2+leng]
        # Подготовить MNIST-изображение
        roi = cv2.resize(roi, (28, 28), interpolation=cv2.INTER_AREA)
        roi = cv2.dilate(roi, (3, 3))
        # Просчитать HOG для областей
        roi_hog_fd = hog(roi, orientations=9, pixels_per_cell=(
                14, 14), cells_per_block=(1, 1), visualize=False, block_norm='L2-Hys')
        nbr = clf.predict(np.array([roi_hog_fd], 'float64'))
        request = nbr[0]
        print("I: Number: ",nbr[0])
        cv2.putText(im, str(int(nbr[0])), (rect[0], rect[1]),cv2.FONT_HERSHEY_DUPLEX, 2, (0, 255, 255), 3)
    except IOError as err:
        print("I/O error: {0}".format(err))
    except ValueError:
        print("Could not convert data to an integer.")
    except:
        print("Unexpected error:", sys.exc_info()[0])
        raise
        


    #cv2.imshow("Resulting Image with Rectangular ROIs", im)
    cv2.imwrite('image_mnist.bmp', im)
    #cv2.waitKey()
      
    #print("I: Normal request")
    server.send(request)

server.close()
context.term()
